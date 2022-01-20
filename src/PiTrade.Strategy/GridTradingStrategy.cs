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
    private class Grid {
      public decimal Price { get; set; }
      public Order? BuyOrder { get; set; }
      public Order? SellOrder { get; set; }
      public Grid(decimal price) : this(price, null) { }
      public Grid(decimal price, Order? order) {
        Price = price;
        BuyOrder = order;
      }
    }

    private readonly int buyGridCount;
    private readonly decimal quotePerGrid;
    private readonly decimal sellThreshold;
    private readonly decimal buyGridDistance;
    private readonly IIndicator indicator;
    private readonly IList<Grid> grids;

    private Func<decimal, Task>? State { get; set; }


    private decimal BasePrice { get; set; }
    private decimal ResetUpPrice { get; set; }
    private decimal ResetLowPrice { get; set; }

    private bool IndicatorIsPositive => 
      indicator.IsReady && indicator.Trend > 0;
     

    // TODO: accurate profit calculation

    public GridTradingStrategy(IMarket market, decimal quotePerGrid, decimal sellThreshold, decimal buyGridDistance, int buyGridCount) : base(market) {
      this.buyGridCount = buyGridCount;
      this.quotePerGrid = quotePerGrid;
      this.sellThreshold = sellThreshold;
      this.buyGridDistance = buyGridDistance;
      this.grids = new List<Grid>();

      indicator = new ExponentialMovingAverage(TimeSpan.FromSeconds(10), 42, IndicatorValueType.Average, simulateWithFirstUpdate: true);
      Market.AddIndicator(indicator);
      State = Initialize;
    }

    protected override async Task Update(decimal currentPrice) {
      if (State != null)
        await State(currentPrice);
    }

    private async Task Initialize(decimal currentPrice) {
      await Task.CompletedTask; // need because of the Task return type

      Log.Info($"[{Market.Asset}{Market.Quote}] Init");
      IEnumerable<Order> tmp = Enumerable.Empty<Order>();
      BasePrice = currentPrice;
      ResetUpPrice = currentPrice * (1m + sellThreshold * 2);
      ResetLowPrice = currentPrice * (1m - (buyGridCount + 1) * buyGridDistance);
      InitializeGrids(currentPrice);
      State = Cycle;
    }

    private async Task EmergencySell() {
      var unselledQuantity = grids
        .Where(x => x.SellOrder != null)
        .Select(x => x.SellOrder)
        .Cast<Order>()
        .Where(x => !x.IsFilled)
        .Sum(x => x.Quantity - x.ExecutedQuantity);
      await Market.CreateMarketOrder(OrderSide.SELL, unselledQuantity);
    }

    private async Task Cycle(decimal currentPrice) {
      if (!indicator.IsReady) return;
      if (indicator.Value > ResetUpPrice)
        State = Initialize;
      if (indicator.Value < ResetLowPrice) {
        await EmergencySell();
        State = Initialize;
      }
      if (indicator.IsBearish) return; 

      int hits = 0;
      IList<Grid> hitGrids = new List<Grid>();
      while(hits < grids.Count && grids[hits].Price >= currentPrice) {
        hitGrids.Add(grids[hits]);
        ++hits;
      }

      // found next buy order
      if (hits > 0) {
        await SetupBuyOrder(currentPrice, hitGrids);
      }
    }

    private void InitializeGrids(decimal basePrice) {
      // cancel old/pending buy orders
      foreach (var grid in grids)
        grid.BuyOrder?.Cancel();
      // clear all
      grids.Clear();
      // add new grids
      for (int i = 0; i < buyGridCount; ++i) {
        var mult = 1.0m - i * buyGridDistance;
        var price = basePrice * mult;
        grids.Add(new Grid(price, null));
      }
    }

    private async Task SetupBuyOrder(decimal price, IEnumerable<Grid> hitGrids) {
      var hits = hitGrids.Count();
      var quantity = GetQuantity(price) * hits;

      // adjust quantity to be greater or equal the quotePerGrid
      while (price * quantity < quotePerGrid) quantity *= 1.001m;

      Log.Info($"[{Market.Asset}{Market.Quote}] BUY price = {price}, hits = {hits}");
      (Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.BUY, price, quantity);
      if (error == ErrorState.None && order != null) {
        foreach (var grid in hitGrids)
          grid.BuyOrder = order;

        order.WhenFilled(BuyFinished);
      } else {
        Log.Error($"Error for SELL -> {error}");
      }
    }

    private async Task SetupSellOrder(Order buyOrder) {
      var price = GetSellPrice(buyOrder.AvgFillPrice);
      var quantity = buyOrder.Quantity;


      // adjust price to be greater or equal the quotePerGrid
      while (price * quantity < quotePerGrid) price *= 1.001m;

      Log.Info($"[{Market.Asset}{Market.Quote}] SELL price = {price}");
      (Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.SELL, price, quantity);
      if (error == ErrorState.None && order != null) {
        foreach(var grid in grids.Where(x => x.BuyOrder == buyOrder).ToArray())
          grid.SellOrder = order;
        order.WhenFilled(SellFinished);
      } else {
        Log.Error($"Error for SELL -> {error}");
      }
    }

    private async Task BuyFinished(Order buyOrder) {
      Log.Info($"[{Market.Asset}{Market.Quote}] BUY Finished price = {buyOrder.AvgFillPrice}");
      await AddFilledOrder(buyOrder);
      await SetupSellOrder(buyOrder);
    }

    private async Task SellFinished(Order sellOrder) {
      Log.Info($"[{Market.Asset}{Market.Quote}] SELL Finished price = {sellOrder.AvgFillPrice}");
      await AddFilledOrder(sellOrder);
      // cancel all buy orders which have lower price than sellOrder
      foreach (var grid in grids.Where(x => x.Price < sellOrder.TargetPrice && x.BuyOrder != null).ToArray())
        await (grid.BuyOrder?.Cancel() ?? Task.CompletedTask);
      LogProfit();
    }

    private void LogProfit() {
      var filledOrders = grids
        .Where(
          x => x.BuyOrder != null && x.SellOrder != null &&
          x.BuyOrder.IsFilled && x.SellOrder.IsFilled)
        .SelectMany(x => new Order[] { x.BuyOrder, x.SellOrder })
        .Cast<Order>()
        .Distinct()
        .ToArray();
      decimal revenue = 0m;
      decimal commission = 0m;
      foreach (var order in filledOrders) { 
        commission += order.Amount * CommissionFee;
        if (order.Side == OrderSide.BUY)
          revenue -= order.Amount;
        else
          revenue += order.Amount;
      }

      Log.Info($"[{Market.Asset}{Market.Quote}] Profit = {revenue - commission}");
    }

    private decimal GetQuantity(decimal price) => quotePerGrid / price;
    private decimal GetSellPrice(decimal price) => price * (1m + sellThreshold);
  }
}
