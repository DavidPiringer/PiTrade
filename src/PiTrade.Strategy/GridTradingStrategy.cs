using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Base;
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
      public IOrder? BuyOrder { get; set; }
      public IOrder? SellOrder { get; set; }
      public bool IsFree { get; set; } = true;
      private Grid() { }
      public Grid(decimal price) : this(price, null) { }
      public Grid(decimal price, IOrder? order) {
        Price = price;
        BuyOrder = order;
      }
      public override string ToString() => $"GRID Price = [{Price}] | BuyOrder = [{BuyOrder}] | SellOrder = [{SellOrder}]";
    }

    public event Action<GridTradingStrategy, decimal>? Profit;
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
    private readonly object locker = new object();

    private decimal LastPrice { get; set; } = decimal.MinValue;

         
    // TODO: add "WhenError" for orders -> put order creation on market into order
    // TODO: add "HandleCommission" for market -> handles commission and maybe change quantity of order

    // TODO: overseer strategy -> watches all markets -> good uptrends -> start grid trading

    public GridTradingStrategy(IMarket market, decimal quotePerGrid, decimal highPrice, decimal lowPrice, uint gridCount, decimal sellThreshold, bool autoDisable = true) /*: base(market)*/ {
      if (lowPrice > highPrice)
        throw new ArgumentException("LowPrice has to be lower than HighPrice.");

      var gridMaxPrice = highPrice / (1m + sellThreshold);
      var gridMinprice = lowPrice;
      if (gridMinprice > gridMaxPrice)
        throw new ArgumentException("Cannot initialize grids: the gap between high and low price is to narrow.");

      this.market = market;
      this.quotePerGrid = quotePerGrid;
      this.highPrice = highPrice;
      this.lowPrice = lowPrice;
      this.sellThreshold = sellThreshold;
      this.autoDisable = autoDisable;
      this.grids = NumSpace.Linear(gridMaxPrice, gridMinprice, gridCount)
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

    private async Task<decimal> GetRestQuantityAndCancel(IOrder? order) {
      if (order != null && order.State == OrderState.Open) {
        await order.Cancel();
        return order.ExecutedQuantity;
      }
      return 0m;
    }

    private async Task OnPriceChanged(IMarket market, decimal price) {
      if(!IsEnabled) return;

      if (autoDisable && (price > highPrice || lowPrice > price))
        await Disable();

      Grid[] hits = new Grid[0];
      lock(locker) {
        hits = grids.Where(x => 
          LastPrice > x.Price && 
          x.Price >= price && 
          x.IsFree).ToArray();

        LastPrice = price;
        foreach (var hit in hits)
          hit.IsFree = false;
      }

      if (hits.Length > 0) await Buy(market, price, hits);
    }

    private async Task Buy(IMarket market, decimal price, IEnumerable<Grid> hits) {
      Log.Info($"[{market.Asset}{market.Quote}] BUY (price = {price})");

      var hitCount = hits.Count();
      var quantity = GetQuantity(price) * hitCount;

      // adjust quantity to be greater or equal the quotePerGrid
      while (price.RoundDown(market.QuotePrecision) * quantity.RoundDown(market.AssetPrecision) < quotePerGrid) 
        quantity *= 1.001m;

      IOrder order = await market.CreateLimitOrder(OrderSide.BUY, price, quantity);
      lock (locker) {
        foreach (var hit in hits)
          hit.BuyOrder = order;
      }
      await order.WhenFilled(async o => {
        await CommissionManager.ManageCommission(o);
        await Sell(market, o, hits);
      });

      await order.WhenCanceled(o => ClearGrids(hits));
    }

    private async Task Sell(IMarket market, IOrder buyOrder, IEnumerable<Grid> hits) {
      Log.Info($"[{market.Asset}{market.Quote}] SELL");

      var price = GetSellPrice(buyOrder.AvgFillPrice);
      var quantity = buyOrder.Quantity;


      // adjust quantity to be greater or equal the quotePerGrid
      while (price.RoundDown(market.QuotePrecision) * quantity.RoundDown(market.AssetPrecision) < quotePerGrid)
        price *= 1.001m;

      IOrder order = await market.CreateLimitOrder(OrderSide.SELL, price, quantity);
      foreach (var hit in hits)
        hit.SellOrder = order;

      await order.WhenFilled(async o => {
        await CommissionManager.ManageCommission(o);
        ClearGrids(hits);
        Log.Info($"SOLD [{o}]");
        Profit?.Invoke(this, o.Amount - buyOrder.Amount);
      });

      await order.WhenCanceled(o => ClearGrids(hits));
    }

    private void ClearGrids(IEnumerable<Grid> grids) {
      lock (locker) {
        foreach (var grid in grids) {
          grid.BuyOrder = null;
          grid.SellOrder = null;
          grid.IsFree = true;
        }
      }
    }

    private decimal GetQuantity(decimal price) => quotePerGrid / price;
    private decimal GetSellPrice(decimal price) => price * (1m + sellThreshold);

    private void PrintGrids() {
      foreach (var grid in grids)
        Log.Info($"[{market.Asset}{market.Quote}] {grid}");
    }
  }
}
