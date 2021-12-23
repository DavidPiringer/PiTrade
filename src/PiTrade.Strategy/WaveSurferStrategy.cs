using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy.Util;

namespace PiTrade.Strategy {
  public class WaveSurferStrategy : Stategy {
    private const decimal CommissionFee = 0.00075m;
    private const decimal Threshold = 0.0016m;

    private readonly IIndicator ema5T60;
    private readonly IIndicator ema5T120;
    private readonly decimal allowedQuote;
    private readonly object locker = new object();

    private decimal lastEma15T8;
    private Order? buyOrder;
    private Order? sellOrder;

    private decimal Quantity { get; set; } = 0m;
    private decimal Commission { get; set; } = 0m;
    private decimal Revenue { get; set; } = 0m;

    public WaveSurferStrategy(IMarket market, decimal allowedQuote, bool respendMoney) : base(market) { 
      ema5T60 = new ExponentialMovingAverage(TimeSpan.FromSeconds(5), 60);
      ema5T120 = new ExponentialMovingAverage(TimeSpan.FromSeconds(5), 120);
      market.AddIndicator(ema5T60);
      market.AddIndicator(ema5T120);
      this.allowedQuote = allowedQuote;
    }

    public override async Task OnBuy(Order order) {
      var lastFill = order.Fills.LastOrDefault();
      if(lastFill != null) {
        lock(locker) {
          Quantity += lastFill.Quantity;
          Quantity = Quantity.RoundDown(Market.AssetPrecision);
          Commission += lastFill.Amount * CommissionFee;
          Revenue -= lastFill.Amount;
        }
      }
      
      await Task.CompletedTask;
    }

    public override async Task OnPriceUpdate(decimal price) {
      if (Handle == null) return;

      if(ema5T60.IsReady && lastEma15T8 != ema5T60.Value && ema5T120.IsReady) {
        var minSellPrice = buyOrder != null && buyOrder.IsFilled ? buyOrder.AvgFillPrice * (1m + Threshold) : 0m;
        Log.Info(
          $"price = {price.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"ema5T60 = {ema5T60.Slope.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"ema5T120 = {ema5T120.Slope.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"minSellPrice = {minSellPrice.ToString("F2", CultureInfo.InvariantCulture)}");
        lastEma15T8 = ema5T60.Value;

        if(buyOrder == null && ema5T60.Slope > 0 && ema5T120.Slope > 0) {
          buyOrder = await Handle.BuyLimit(price, allowedQuote / price);
          Log.Info("BUY");
        } else if (buyOrder != null && buyOrder.IsFilled && 
          sellOrder == null &&
          price > buyOrder.AvgFillPrice * (1m + Threshold) &&
          ema5T60.Slope < 0) {
          sellOrder = await Handle.Market(OrderSide.SELL, Quantity);
          Log.Info("SELL");
        }
      }

      if(buyOrder != null && buyOrder.IsFilled && 
        sellOrder == null &&
        price < buyOrder.AvgFillPrice * (1m - Threshold)) {
        sellOrder = await Handle.Market(OrderSide.SELL, Quantity);
        Log.Warn($"Emergency Sell, Price Dropped too fast (cur. Price = {price}, Buy Price = {buyOrder.AvgFillPrice})");
      }
    }

    public override async Task OnSell(Order order) {
      var lastFill = order.Fills.LastOrDefault();
      if (lastFill != null) {
        lock (locker) {
          Quantity -= lastFill.Quantity;
          Quantity = Quantity.RoundDown(Market.AssetPrecision); 
          Commission += lastFill.Amount * CommissionFee;
          Revenue += lastFill.Amount;
        }
      }
      if (order.IsFilled) {
        Log.Info($"CLEAR {Market.Asset}{Market.Quote}");
        try {
          await CommissionManager.Add(Commission);
        } catch (Exception ex) {
          Log.Error(ex);
        }
        lock (locker) {
          buyOrder = null;
          sellOrder = null;
          ProfitCalculator.Add(Revenue - Commission);
          ProfitCalculator.PrintStats();
          Revenue = 0m;
          Commission = 0m;
        }
      }
    }
  }
}
