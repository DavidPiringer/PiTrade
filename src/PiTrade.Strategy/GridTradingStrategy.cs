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
using PiTrade.Strategy.ConfigDTOs;
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
      public decimal Quote { get; set; }
      private Grid() { }
      public Grid(decimal price, decimal quote) {
        Price = price;
        Quote = quote;
      }
      public override string ToString() => $"GRID Price = [{Price}] | BuyOrder = [{BuyOrder}] | SellOrder = [{SellOrder}]";
    }

    private readonly IMarket market;
    private readonly decimal minQuotePerGrid;
    private readonly decimal reinvestProfitRatio;
    private readonly decimal highPrice;
    private readonly decimal lowPrice;
    private readonly decimal sellThreshold;
    private readonly bool autoDisable;
    private readonly Grid[] grids;
    private readonly object locker = new object();

    private decimal LastPrice { get; set; } = decimal.MinValue;

    public bool IsEnabled { get; set; }

    public static decimal Profit { get; private set; }

    public GridTradingStrategy(IMarket market, GridTradingStrategyConfig config) :
      this(market, 
        config.MinQuotePerGrid, config.ReinvestProfitRatio, 
        config.HighPrice, config.LowPrice, 
        config.GridCount, config.SellThreshold, 
        config.AutoDisable) { }

    public GridTradingStrategy(IMarket market,
      decimal minQuotePerGrid, decimal reinvestProfitRatio,
      decimal p1, decimal p2,
      uint gridCount, decimal sellThreshold,
      bool autoDisable = true) {

      if (reinvestProfitRatio < 0 || reinvestProfitRatio > 1.0m)
        throw new ArgumentException("Reinvest Profit Ratio needs to be between 0.0 and 1.0.");
      if (gridCount == 0)
        throw new ArgumentException("Grid Count needs to be higher than 0.");


      this.market = market;
      this.minQuotePerGrid = minQuotePerGrid;
      this.reinvestProfitRatio = reinvestProfitRatio;
      this.highPrice = Math.Max(p1, p2);
      this.lowPrice = Math.Min(p1, p2);
      this.sellThreshold = sellThreshold;
      this.autoDisable = autoDisable;
      this.grids = NumSpace.Linear(highPrice, lowPrice, gridCount)
                           .Select(x => new Grid(x, minQuotePerGrid)).ToArray();
      PrintGrids();
    }

    public void Enable() {
      IsEnabled = true;
      market.SubscribeAsync(OnPriceChanged);
    }

    public async Task Disable(bool sellAll = true) {
      IsEnabled = false;
      market.UnsubscribeAsync(OnPriceChanged);

      if (sellAll) {
        IList<Task> tasks = new List<Task>();
        foreach (var grid in grids) {
          tasks.Add(CancelAndSell(grid.BuyOrder));
          tasks.Add(CancelAndSell(grid.SellOrder));
          grid.BuyOrder = null;
          grid.SellOrder = null;
        }
        await Task.WhenAll(tasks);
      }
    }

    private async Task CancelAndSell(IOrder? order) {
      if (order != null && order.State == OrderState.Open) {
        Log.Warn($"[{market.QuoteAsset}{market.BaseAsset}] Cancel & Sell [{order}]");
        await order.Cancel(); // TODO: Fix selling
       // await order.WhenCanceled(async x => await market.CreateMarketOrder(OrderSide.SELL, x.ExecutedQuantity));
      }
    }

    private async Task OnPriceChanged(ITrade trade) {
      if (!IsEnabled) return;

      var price = trade.Price;

      if (autoDisable && (price > highPrice || lowPrice > price))
        await Disable();

      Grid[] hits = new Grid[0];
      lock (locker) {
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
      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] BUY (price = {price})");

      var quote = hits.Sum(x => x.Quote);
      var hitCount = hits.Count();
      var quantity = GetQuantity(quote, price) * hitCount;

      // adjust quantity to be greater or equal the quotePerGrid
      while (price.RoundDown(market.BaseAssetPrecision) * quantity.RoundDown(market.QuoteAssetPrecision) < quote)
        quantity *= 1.001m;

      IOrder order = await market
        .Buy(quantity)
        .For(price)
        .OnExecutedAsync(o => Sell(market, o, hits))
        .OnCancel(o => { lock (locker) ClearGrids(hits); })
        .Submit();

      lock (locker) {
        foreach (var hit in hits)
          hit.BuyOrder = order;
      }
    }

    private async Task Sell(IMarket market, IOrder buyOrder, IEnumerable<Grid> hits) {

      var quote = AddSellThreshold(hits.Sum(x => x.Quote));
      var price = AddSellThreshold(buyOrder.ExecutedPrice);
      var quantity = buyOrder.Quantity;

      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] SELL (price = {price})");

      // adjust quantity to be greater or equal the quotePerGrid
      while (price.RoundDown(market.BaseAssetPrecision) * quantity.RoundDown(market.QuoteAssetPrecision) < quote)
        price *= 1.001m;

      IOrder order = await market
        .Sell(quantity)
        .For(price)
        .OnExecuted(o => {
          lock (locker) {
            var profit = o.ExecutedAmount - buyOrder.ExecutedAmount; // TODO: Add Commission
            Profit += profit;
            ClearGrids(hits);
          }
          Log.Info($"SOLD [{o}] Profit = {Profit}");
        })
        .OnCancel(o => ClearGrids(hits))
        .Submit();

      foreach (var hit in hits)
        hit.SellOrder = order;
    }

    private void ClearGrids(IEnumerable<Grid> grids) {
      foreach (var grid in grids) {
        grid.BuyOrder = null;
        grid.SellOrder = null;
        grid.IsFree = true;
      }
    }

    private decimal GetQuantity(decimal quote, decimal price) => quote / price;
    private decimal AddSellThreshold(decimal price) => price * (1m + sellThreshold);

    private void PrintGrids() {
      foreach (var grid in grids)
        Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] {grid}");
    }
  }
}
