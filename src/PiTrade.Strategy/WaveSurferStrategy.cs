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
    private const decimal UpperThreshold = 0.002m;
    private const decimal LowerThreshold = 0.01m;

    private readonly IEnumerable<IIndicator> indicators;
    private readonly decimal allowedQuote;
    private readonly object locker = new object();

    private Order? buyOrder;
    private Order? sellOrder;

    private decimal Quantity { get; set; } = 0m;
    private decimal Commission { get; set; } = 0m;
    private decimal Revenue { get; set; } = 0m;


    private Func<Task>? State { get; set; }


    public WaveSurferStrategy(IMarket market, decimal allowedQuote, bool respendMoney) : base(market) {
      State = PrepareBuy;

      indicators = new IIndicator[] {
        new ExponentialMovingAverage(TimeSpan.FromSeconds(5), 12),  // 1min
        new ExponentialMovingAverage(TimeSpan.FromSeconds(5), 120), // 10min
        new ExponentialMovingAverage(TimeSpan.FromSeconds(5), 300)  // 25min
      };

      foreach(var indicator in indicators)
        market.AddIndicator(indicator);

      market.Listen(MarketListener);
      this.allowedQuote = allowedQuote;
    } // TODO: add max. quote to fail for




    // TODO: add PiTrade.EventHandling -> convinient stuff for events

    private async Task MarketListener(decimal price) {
      if (State != null) await State();
      else Log.Info("STATE = null");
    }

    private async Task PrepareBuy() {
      Log.Info("STATE = PrepareBuy");
      if (buyOrder != null) return;

      if (indicators.All(x => x.Slope > 0)) {
        State = null;
        var price = Market.CurrentPrice;
        var quantity = allowedQuote / price;
        Log.Info("BUY");
        (Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.BUY, price, quantity);
        if(error == ErrorState.None && order != null) {
          this.buyOrder = order;
          order.WhenFilled(BuyFinished);
        } else {
          Log.Error($"Error for BUY -> {error}");
        }
      }
    }

    private async Task PrepareSell() {
      Log.Info("STATE = PrepareSell");
      if (sellOrder != null) return;
      //TODO: add indicator listen to market -> market gives indicator array and current price
      if (buyOrder != null && indicators.Any(x => x.Slope < 0)) {
        var price = Market.CurrentPrice;
        if (price > buyOrder.AvgFillPrice * (1m + UpperThreshold) ||
            price < buyOrder.AvgFillPrice * (1m - LowerThreshold)) {
          State = null;
          Log.Info("SELL");
          (Order? order, ErrorState error) = await Market.CreateMarketOrder(OrderSide.SELL, buyOrder.Quantity);
          if (error == ErrorState.None && order != null) {
            this.sellOrder = order;
            order.WhenFilled(SellFinished);
          } else {
            Log.Error($"Error for SELL -> {error}");
          }
        }
      }
    }

    private void BuyFinished(Order buyOrder) {
      Log.Info("STATE = BuyFinished");
      lock (locker) {
        Quantity += buyOrder.Quantity;
        Quantity = Quantity.RoundDown(Market.AssetPrecision);
        Commission += buyOrder.Amount * CommissionFee;
        Revenue -= buyOrder.Amount;
        State = PrepareSell;
      }
    }

    private void SellFinished(Order sellOrder) {
      Log.Info("STATE = SellFinished");
      lock (locker) {
        Quantity -= sellOrder.Quantity;
        Quantity = Quantity.RoundDown(Market.AssetPrecision);
        Commission += sellOrder.Amount * CommissionFee;
        Revenue += sellOrder.Amount;
        this.buyOrder = null;
        this.sellOrder = null;
        ProfitCalculator.Add(Revenue - Commission);
        ProfitCalculator.PrintStats();
        Revenue = 0m;
        Commission = 0m;
        State = PrepareBuy;
      }
    }
    /*
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

      if(ema5T36.IsReady && lastEma15T8 != ema5T36.Value && ema5T120.IsReady) {
        var minSellPrice = buyOrder != null && buyOrder.IsFilled ? buyOrder.AvgFillPrice * (1m + UpperThreshold) : 0m;
        var emergencySellPrice = buyOrder != null && buyOrder.IsFilled ? buyOrder.AvgFillPrice * (1m - LowerThreshold) : 0m;
        Log.Info(
          $"price = {price.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"ema5T36 = {ema5T36.Slope.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"ema5T120 = {ema5T120.Slope.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"minSellPrice = {minSellPrice.ToString("F2", CultureInfo.InvariantCulture)}, " +
          $"emergencySellPrice = {emergencySellPrice.ToString("F2", CultureInfo.InvariantCulture)}");
        lastEma15T8 = ema5T36.Value;

        if(buyOrder == null && ema5T36.Slope > 0 && ema5T120.Slope > 0) {
          buyOrder = await Handle.BuyLimit(price, allowedQuote / price);
          Log.Info("BUY");
        } else if (!IsSelling && 
          buyOrder != null && buyOrder.IsFilled && 
          sellOrder == null &&
          price > buyOrder.AvgFillPrice * (1m + UpperThreshold) &&
          ema5T36.Slope < 0) {
          IsSelling = true; // is needed because of the async structure
          if (sellOrder2 != null) await Handle.Cancel(sellOrder2);
          sellOrder = await Handle.Market(OrderSide.SELL, buyOrder.Quantity);
          Log.Info("SELL");
        } else if(!IsSelling && buyOrder != null && buyOrder.IsFilled) {
          if (sellOrder2 != null) await Handle.Cancel(sellOrder2);
          sellOrder2 = await Handle.SellLimit(price * (1m + UpperThreshold * 1.5m), buyOrder.Quantity);
        }
      }
      
      if(!IsSelling && 
        buyOrder != null && buyOrder.IsFilled && 
        sellOrder == null &&
        price < buyOrder.AvgFillPrice * (1m - LowerThreshold)) {
        IsSelling = true; // is needed because of the async structure
        if (sellOrder2 != null) await Handle.Cancel(sellOrder2);
        sellOrder = await Handle.Market(OrderSide.SELL, buyOrder.Quantity); // TODO: Order = Task ??? create own TPL with orders? 
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
          sellOrder2 = null;
          IsSelling = false;
          ProfitCalculator.Add(Revenue - Commission);
          ProfitCalculator.PrintStats();
          Revenue = 0m;
          Commission = 0m;
        }
      }
    }*/
  }
}
