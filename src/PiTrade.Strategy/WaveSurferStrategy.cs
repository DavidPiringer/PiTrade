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
    private const decimal UpperThreshold = 0.002m;
    private const decimal LowerThreshold = 0.01m;

    private readonly IEnumerable<IIndicator> indicators;
    private readonly decimal allowedQuote;

    private Order? buyOrder;
    private Order? sellOrder;

    private bool AllIndicatorsReady => indicators.All(x => x.IsReady);
    private bool AllIndicatorsPositive => indicators.All(x => x.Slope > 0);
    private bool AnyIndicatorsNegative => indicators.Any(x => x.Slope < 0);


    private Func<decimal, Task>? State { get; set; }


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

    private async Task MarketListener(decimal currentPrice) {
      if (State != null && AllIndicatorsReady) 
        await State(currentPrice);
      else Log.Info("STATE = null");
    }

    private async Task PrepareBuy(decimal currentPrice) {
      Log.Info("STATE = PrepareBuy");
      if (buyOrder != null) return;

      if (AllIndicatorsPositive) {
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

    private async Task PrepareSell(decimal currentPrice) {
      Log.Info("STATE = PrepareSell");
      if (sellOrder != null) return;

      if (buyOrder != null && AnyIndicatorsNegative &&
         (currentPrice > buyOrder.AvgFillPrice * (1m + UpperThreshold) ||
          currentPrice < buyOrder.AvgFillPrice * (1m - LowerThreshold))) {
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

    private void BuyFinished(Order buyOrder) {
      Log.Info("STATE = BuyFinished");
      AddFilledOrder(buyOrder);
      State = PrepareSell;
    }

    private void SellFinished(Order sellOrder) {
      Log.Info("STATE = SellFinished");
      AddFilledOrder(sellOrder);
      Reset();
      State = PrepareBuy;
    }

    protected override void Reset() {
      base.Reset();
      this.buyOrder = null;
      this.sellOrder = null;
    }
  }
}
