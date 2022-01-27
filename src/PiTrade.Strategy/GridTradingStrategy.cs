using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy.Util;

namespace PiTrade.Strategy {
  // https://www.investopedia.com/terms/g/grid-trading.asp
  /// <summary>
  /// This strategy copies the Against-The-Trend Grid Trading strategy. 
  /// Everytime a buy order is triggered it creates a sell order with a 
  /// certain threshold above the avg. price of the buy order.
  /// </summary>
  public class GridTradingStrategy {
    private class Grid {
      public decimal Price { get; set; }
      public Order? BuyOrder { get; set; }
      public Order? SellOrder { get; set; }
      private Grid() { }
      public Grid(decimal price) : this(price, null) { }
      public Grid(decimal price, Order? order) {
        Price = price;
        BuyOrder = order;
      }
      public override string ToString() => $"GRID Price = [{Price}] | BuyOrder = [{BuyOrder}] | SellOrder = [{SellOrder}]";
    }

    public event Action<GridTradingStrategy, decimal>? Profit;
    public event Action<GridTradingStrategy, decimal>? Commission;
    public event Action<GridTradingStrategy, bool>? EnableChanged;

    private bool isEnabled;
    public bool IsEnabled { 
      get => isEnabled;
      private set {
        isEnabled = value;
        EnableChanged?.Invoke(this, value);
      }
    }

    private readonly IMarket market;
    private readonly decimal quotePerGrid;
    private readonly decimal highPrice;
    private readonly decimal lowPrice;
    private readonly decimal sellThreshold;
    private readonly bool autoDisable;
    private readonly Grid[] grids;

    private decimal LastPrice { get; set; } = decimal.MinValue;

         
    // TODO: add "WhenError" for orders -> put order creation on market into order
    // TODO: add "HandleCommission" for market -> handles commission and maybe change quantity of order

    // TODO: overseer strategy -> watches all markets -> good uptrends -> start grid trading

    public GridTradingStrategy(IMarket market, decimal quotePerGrid, decimal highPrice, decimal lowPrice, uint gridCount, decimal sellThreshold, bool autoDisable = true) /*: base(market)*/ {
      if (lowPrice > highPrice)
        throw new ArgumentException("LowPrice has to be greater than HighPrice.");

      this.market = market;
      this.quotePerGrid = quotePerGrid;
      this.highPrice = highPrice;
      this.lowPrice = lowPrice;
      this.sellThreshold = sellThreshold;
      this.autoDisable = autoDisable;

      this.grids = NumSpace.Linear(highPrice, lowPrice, (gridCount + 1), true)
                           .Select(x => new Grid(x)).ToArray();

      PrintGrids();
    }

    public void Enable() {
      IsEnabled = true;
      market.Register2PriceChanges(OnPriceChanged);
    }

    public async Task Disable(bool sellAll = true) {
      IsEnabled = false;
      market.Unregister2PriceChanges(OnPriceChanged);

      if (sellAll) {
        decimal restQuantity = 0m;
        foreach (var grid in grids) {
          restQuantity += await GetRestQuantityAndCancel(grid.BuyOrder);
          restQuantity += await GetRestQuantityAndCancel(grid.SellOrder);
        }

        if(restQuantity > 0)
          await market.CreateMarketOrder(OrderSide.SELL, restQuantity);
      }
    }

    private async Task<decimal> GetRestQuantityAndCancel(Order? order) {
      if (order != null && !order.IsFilled && !order.IsFaulted) {
        await order.Cancel();
        return order.ExecutedQuantity;
      }
      return 0m;
    }

    private async Task OnPriceChanged(IMarket market, decimal price) {
      if(!IsEnabled) return;

      if (autoDisable && (price > highPrice || lowPrice > price))
        await Disable();

      var hits = grids.Where(x => 
        LastPrice > x.Price && 
        x.Price >= price && 
        x.BuyOrder == null && 
        x.SellOrder == null).ToArray();

      LastPrice = price;
      if (hits.Any()) await Buy(market, price, hits);
      
    }

    private async Task Buy(IMarket market, decimal price, IEnumerable<Grid> hits) {
      Log.Info($"[{market.Asset}{market.Quote}] BUY (price = {price})");

      var hitCount = hits.Count();
      var quantity = GetQuantity(price) * hitCount;

      // adjust quantity to be greater or equal the quotePerGrid
      while (price.RoundDown(market.QuotePrecision) * quantity.RoundDown(market.AssetPrecision) < quotePerGrid) 
        quantity *= 1.001m;

      (Order order, ErrorState error) = await market.CreateLimitOrder(OrderSide.BUY, price, quantity);
      foreach(var hit in hits) {
        hit.BuyOrder = order;

      }
      await order.WhenFilled(async o => {
        AddCommission(o);
        await Sell(market, o, hits);
      });
      /*
      order.WhenFaulted(o => {
        foreach (var hit in hits)
          hit.BuyOrder = null;
      });*/
    }

    private async Task Sell(IMarket market, Order buyOrder, IEnumerable<Grid> hits) {
      Log.Info($"[{market.Asset}{market.Quote}] SELL");

      var price = GetSellPrice(buyOrder.AvgFillPrice);
      var quantity = buyOrder.Quantity;


      // adjust quantity to be greater or equal the quotePerGrid
      while (price.RoundDown(market.QuotePrecision) * quantity.RoundDown(market.AssetPrecision) < quotePerGrid)
        price *= 1.001m;

      (Order order, ErrorState error) = await market.CreateLimitOrder(OrderSide.SELL, price, quantity);
      foreach (var hit in hits)
        hit.SellOrder = order;

      await order.WhenFilled(o => {
        foreach (var hit in hits) {
          hit.BuyOrder = null;
          hit.SellOrder = null;
        }
        Log.Info($"SOLD [{o}]");
        PrintGrids();
        AddCommission(o);
        Profit?.Invoke(this, o.Amount - buyOrder.Amount);
      });
      /*
      order.WhenFaulted(o => {
        foreach (var hit in hits)
          hit.SellOrder = null;
      });*/

    }

    private void AddCommission(Order order) {
      Commission?.Invoke(this, order.Amount * 0.0075m);
    }
    private decimal GetQuantity(decimal price) => quotePerGrid / price;
    private decimal GetSellPrice(decimal price) => price * (1m + sellThreshold);

    private void PrintGrids() {
      foreach (var grid in grids)
        Log.Info($"[{market.Asset}{market.Quote}] {grid}");
    }
    /*
    protected override async Task Update(decimal currentPrice) {
      if (State != null)
        await State(currentPrice);
    }*/
    /*
    private async Task Initialize(decimal currentPrice) {
      await Task.CompletedTask; // need because of the Task return type

      Log.Info($"[{Market.Asset}{Market.Quote}] Init");
      IEnumerable<Order> tmp = Enumerable.Empty<Order>();
      ResetUpPrice = currentPrice * (1m + sellThreshold * 2);
      ResetLowPrice = currentPrice * (1m - (buyGridCount + 2) * buyGridDistance);
      InitializeGrids(currentPrice);
      State = Cycle;
    }

    private async Task EmergencySell() {
      Log.Error($"[{Market.Asset}{Market.Quote}] EMERGENCY SELL");
      var openSellOrders = grids
        .Where(x => x.SellOrder != null)
        .Select(x => x.SellOrder)
        .Cast<Order>()
        .Where(x => !x.IsFilled)
        .ToList();

      foreach (var sellOrder in openSellOrders)
        await sellOrder.Cancel();

      var unselledQuantity = openSellOrders.Sum(x => x.Quantity - x.ExecutedQuantity);
      (Order? order, ErrorState error) = await Market.CreateMarketOrder(OrderSide.SELL, unselledQuantity);
      if (error == ErrorState.None && order != null) {
        order.WhenFilled(EmergencySellFinished);
      }
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

      //if(Market.Asset == Exchange.Entities.Symbol.ETH)
        //Log.Info($"[{Market.Asset}{Market.Quote}] Indicator = {indicator.Value} {indicator.Trend} {indicator.Slope}");

      var hits = grids
        .Where(x => x.Price >= currentPrice && 
                    x.BuyOrder == null && 
                    x.SellOrder == null);

      // found next buy order
      if (hits.Any()) await SetupBuyOrder(currentPrice, hits);
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
      Log.Info($"[{Market.Asset}{Market.Quote}] Indicator = {indicator.Value} {indicator.Trend} {indicator.Slope}");

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
        await Task.Delay(TimeSpan.FromSeconds(10)); // TODO: handle better
      }
    }

    private async Task SetupSellOrder(Order buyOrder) {
      var price = GetSellPrice(buyOrder.AvgFillPrice);
      var quantity = buyOrder.Quantity;


      // adjust price to be greater or equal the quotePerGrid
      while (price * quantity < quotePerGrid) price *= 1.001m;

      (Order? order, ErrorState error) = await Market.CreateLimitOrder(OrderSide.SELL, price, quantity);
      if (error == ErrorState.None && order != null) {
        foreach(var grid in grids.Where(x => x.BuyOrder == buyOrder).ToArray())
          grid.SellOrder = order;
        order.WhenFilled(SellFinished);
      } else {
        Log.Error($"Error for SELL -> {error}");
        await Task.Delay(TimeSpan.FromSeconds(10)); // TODO: handle better
      }
    }

    private async Task BuyFinished(Order buyOrder) {
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

    private async Task EmergencySellFinished(Order sellOrder) {
      Log.Info($"[{Market.Asset}{Market.Quote}] EMERGENCY SELL Finished price = {sellOrder.AvgFillPrice}");
      await AddFilledOrder(sellOrder);
      emergencySells.Add(sellOrder);
      LogProfit();
    }

    private void LogProfit() {
      var filledOrders = grids
        .Where(
          x => x.BuyOrder != null && x.SellOrder != null &&
          x.BuyOrder.IsFilled && x.SellOrder.IsFilled)
        .SelectMany(x => new Order?[] { x.BuyOrder, x.SellOrder })
        .Cast<Order>()
        .Union(emergencySells)
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
    
    */
  }
}
