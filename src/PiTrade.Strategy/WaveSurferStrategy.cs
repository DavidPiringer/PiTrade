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
  public class WaveSurferStrategy : Strategy {
    private const decimal UpperThreshold = 0.0025m;
    private const decimal LowerThreshold = 0.0025m;

    private readonly IEnumerable<IIndicator> indicators;
    private readonly decimal allowedQuote;

    private Order? buyOrder;
    private Order? sellOrder;
    private bool emergencyStop;

    private bool AllIndicatorsReady => indicators.All(x => x.IsReady);
    private bool AllIndicatorsPositive(decimal currentPrice) => indicators.All(x => x.Slope > 0.5);
    private bool AnyIndicatorsNegative(decimal currentPrice) => indicators.Any(x => x.Slope < 0);


    private Func<decimal, Task>? State { get; set; }


    public WaveSurferStrategy(IMarket market, decimal allowedQuote, bool respendMoney) : base(market) {
      State = PrepareBuy;

      indicators = new IIndicator[] {
        //new SimpleMovingAverage(TimeSpan.FromMinutes(1), 9, IndicatorValueType.Close, simulateWithFirstUpdate: false),
        new ExponentialMovingAverage(TimeSpan.FromMinutes(1), 12, IndicatorValueType.Close, simulateWithFirstUpdate: true),
        new ExponentialMovingAverage(TimeSpan.FromMinutes(1), 60, IndicatorValueType.Close, simulateWithFirstUpdate: true)
      };

      foreach(var indicator in indicators)
        market.AddIndicator(indicator);

      this.allowedQuote = allowedQuote;
    } // TODO: add max. quote to fail for

    // TODO: add PiTrade.EventHandling -> convinient stuff for events

    protected override async Task Update(decimal currentPrice) {
      if (!AllIndicatorsReady) {
        Log.Info($"Preparing Indicators ...");
        return;
      }

      // TODO: ConsoleMonitor -> ConsoleGUI
      //foreach (var indicator in indicators)
      //  Log.Info($"[{Market.Asset}/{Market.Quote}] {indicator.Period.TotalMinutes}m {indicator.Slope}");


      if (State != null) 
        await State(currentPrice);
    }

    private async Task PrepareBuy(decimal currentPrice) {
      if (buyOrder != null) return;

      if (AllIndicatorsPositive(currentPrice)) {
        State = null;
        var quantity = allowedQuote / currentPrice;
        Log.Info($"BUY {Market.Asset}/{Market.Quote}");
        // TODO: update pooling in order
        //(Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.BUY, currentPrice, quantity);
        (Order? order, ErrorState error) = await Market.CreateMarketOrder(OrderSide.BUY, quantity);
        if (error == ErrorState.None && order != null) {
          this.buyOrder = order;
          order.WhenFilled(BuyFinished);
        } else {
          Log.Error($"Error for BUY -> {error}");
        }
      }
    }

    private async Task PrepareSell(decimal currentPrice) {
      if (sellOrder != null || buyOrder == null) return;

      var isProfitableTrade = currentPrice > buyOrder.AvgFillPrice * (1m + UpperThreshold);
      var isUnprofitableTrade = currentPrice < buyOrder.AvgFillPrice * (1m - LowerThreshold);
      if (AnyIndicatorsNegative(currentPrice) &&
         (isProfitableTrade || isUnprofitableTrade)) {
        State = null;

        (Order? order, ErrorState error) = await Market.CreateMarketOrder(OrderSide.SELL, buyOrder.Quantity);
        Log.Info($"SELL {Market.Asset}/{Market.Quote} (profitable = {isProfitableTrade})");

        if (error == ErrorState.None && order != null) {
          this.sellOrder = order;
          order.WhenFilled(SellFinished);
        } else {
          Log.Error($"Error for SELL -> {error}");
        }
      }
    }

    private void BuyFinished(Order buyOrder) {
      Log.Info("BuyFinished");
      AddFilledOrder(buyOrder);
      State = PrepareSell;
    }

    private void SellFinished(Order sellOrder) {
      Log.Info("SellFinished");
      AddFilledOrder(sellOrder);
      Reset();
    }

    protected override void EmergencyStop() {
      base.EmergencyStop();
      emergencyStop = true;
      Task? buyCancel = buyOrder?.Cancel();
      Task? sellCancel = sellOrder?.Cancel();
      buyCancel?.Wait();
      sellCancel?.Wait();
    }

    protected override void Reset() {
      base.Reset();

      if (buyOrder != null && sellOrder != null) {
        Log.Info($"[{Market.Asset}/{Market.Quote}] {buyOrder.Amount} < {sellOrder.Amount} " +
          $"(AVG = {buyOrder.AvgFillPrice} < {sellOrder.AvgFillPrice}) " +
          $"(Target = {buyOrder.TargetPrice} < {sellOrder.TargetPrice})");
      }

      this.buyOrder = null;
      this.sellOrder = null;
      if(!emergencyStop)
        State = PrepareBuy;
    }
  }
}
