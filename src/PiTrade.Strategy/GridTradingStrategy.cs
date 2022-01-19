using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy.Util;

namespace PiTrade.Strategy {
  // https://www.investopedia.com/terms/g/grid-trading.asp
  /// <summary>
  /// This strategy copies the Against-The-Trend Grid Trading strategy but 
  /// it creates only a buy order grid. Everytime a buy order is triggered
  /// it creates a sell order with a certain threshold above the avg. price
  /// of the buy order. Additionally, it holds the buys if a strong down-trend
  /// is occuring.
  /// </summary>
  public class GridTradingStrategy : Strategy {
    private readonly object locker = new object();
    private readonly int buyGridCount;
    private readonly decimal quotePerGrid;
    private readonly decimal sellThreshold;
    private readonly IIndicator indicator;
    private readonly IDictionary<Order, int> hitDict = new Dictionary<Order, int>();


    private Func<decimal, Task>? State { get; set; }


    private decimal BasePrice { get; set; }
    private decimal ResetPrice { get; set; }
    private int CurrentGridIndex { get; set; }
    private decimal CurrentBuyPrice => BasePrice * (1m - CurrentGridIndex * sellThreshold);
    private bool IndicatorIsPositive => indicator.IsReady && indicator.Trend > 0;
     

    public GridTradingStrategy(IMarket market, decimal quotePerGrid, decimal sellThreshold, int buyGridCount) : base(market) {
      this.buyGridCount = buyGridCount;
      this.quotePerGrid = quotePerGrid;
      this.sellThreshold = sellThreshold;
      indicator = new ExponentialMovingAverage(TimeSpan.FromMinutes(1), 12, IndicatorValueType.Close, simulateWithFirstUpdate: true);
      Market.AddIndicator(indicator);
      State = Init;
    }

    protected override async Task Update(decimal currentPrice) {
      if (State != null)
        await State(currentPrice);
    }

    private async Task Init(decimal currentPrice) {
      await Task.CompletedTask;
      lock (locker) {
        BasePrice = currentPrice;
        ResetPrice = currentPrice * (1m + sellThreshold * 2);
        CurrentGridIndex = 0;
      }
      
      State = Cycle;
    }

    private async Task Cycle(decimal currentPrice) {
      if (currentPrice > ResetPrice)
        State = Init;

      if (IndicatorIsPositive) return;

      int hits = 0;
      lock (locker) {
        while (currentPrice < CurrentBuyPrice && CurrentGridIndex++ < buyGridCount)
          hits++;
      }

      // found next buy order
      if (hits > 0) {
        await SetupBuyOrder(currentPrice, hits);
      }
    }


    private async Task SetupBuyOrder(decimal price, int multiplier) {
      Log.Info($"[BUY] price = {price}, multiplier = {multiplier}");
      var quantity = GetQuantity(price) * multiplier;

      // adjust quantity to be greater or equal the quotePerGrid
      while (price * quantity < quotePerGrid) quantity *= 1.001m;

      (Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.BUY, price, quantity);
      if (error == ErrorState.None && order != null) {
        lock (locker) hitDict.Add(order, multiplier);
        order.WhenFilled(BuyFinished);
      }
    }

    private async Task SetupSellOrder(Order buyOrder) {
      var price = GetSellPrice(buyOrder.AvgFillPrice);
      var quantity = buyOrder.Quantity;
      
      // adjust price to be greater or equal the quotePerGrid
      while (price * quantity < quotePerGrid) price *= 1.001m;

      (Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.BUY, price, quantity);
      if (error == ErrorState.None && order != null) {
        lock (locker) {
          if (hitDict.TryGetValue(buyOrder, out var hit)) {
            hitDict.Remove(buyOrder);
            hitDict.Add(order, hit);
          }
        }
        order.WhenFilled(SellFinished);
      }
    }

    private async Task BuyFinished(Order buyOrder) {
      AddFilledOrder(buyOrder);
      await SetupSellOrder(buyOrder);
    }

    private void SellFinished(Order sellOrder) {
      AddFilledOrder(sellOrder);
      lock (locker) {
        if (hitDict.TryGetValue(sellOrder, out var hit)) {
          hitDict.Remove(sellOrder);
          CurrentGridIndex -= hit;
        }
      }
      PrintStatus();
    }

    private decimal GetQuantity(decimal price) => quotePerGrid / price;
    private decimal GetSellPrice(decimal price) => price * (1m + sellThreshold);
  }
}
