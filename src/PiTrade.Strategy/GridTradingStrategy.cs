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
      public decimal Quantity { get; set; }
      private Grid() { }
      public Grid(decimal price, decimal amount) {
        Price = price;
        Quantity = amount / price;
      }
      public override string ToString() => $"GRID Price = [{Price}] | BuyOrder = [{BuyOrder}] | SellOrder = [{SellOrder}]";
    }

    private readonly IMarket market;
    private readonly decimal highPrice;
    private readonly decimal lowPrice;
    private readonly decimal sellThreshold;
    private readonly bool autoStop;
    private readonly Grid[] grids;

    private decimal LastPrice { get; set; } = decimal.MinValue;

    public bool IsRunning { get; set; }

    public static decimal Profit { get; private set; }

    public GridTradingStrategy(IMarket market,
      decimal minAmountPerGrid,
      decimal p1, decimal p2,
      uint gridCount, decimal sellThreshold,
      bool autoStop = true) {

      if (gridCount == 0)
        throw new ArgumentException("Grid Count needs to be higher than 0.");

      this.market = market;
      this.highPrice = Math.Max(p1, p2);
      this.lowPrice = Math.Min(p1, p2);
      this.sellThreshold = sellThreshold;
      this.autoStop = autoStop;
      this.grids = NumSpace.Linear(highPrice, lowPrice, gridCount)
                           .Select(x => new Grid(x, minAmountPerGrid)).ToArray();
      PrintGrids();
    }

    public void Start() {
      IsRunning = true;
      market.Subscribe(OnPriceChanged);
    }

    public void Stop() {
      IsRunning = false;
      market.Unsubscribe(OnPriceChanged);
    }

    private void OnPriceChanged(ITrade trade) {
      if (!IsRunning) return;

      var price = trade.Price;

      if (CancelCondition(trade))
        Stop();

      Grid[] hits = grids
        .Where(x => 
          LastPrice > x.Price && 
          x.Price >= price &&
          x.BuyOrder == null &&
          x.SellOrder == null)
        .ToArray();

      LastPrice = price;

      if (hits.Length > 0) Buy(market, price, hits);
    }

    private void Buy(IMarket market, decimal price, IEnumerable<Grid> hits) {
      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] BUY (price = {price})");

      var quantity = hits.Sum(x => x.Quantity);

      IOrder order = market
        .Buy(quantity)
        .For(price)
        .OnExecuted(o => Sell(o, hits))
        .OnCancel(o => ClearBuyGrids(hits))
        .CancelIf((o,t) => CancelCondition(t))
        .Submit();

      foreach (var hit in hits)
        hit.BuyOrder = order;
    }

    private void ClearBuyGrids(IEnumerable<Grid> hits) {
      foreach (var hit in hits)
        hit.BuyOrder = null;
    }

    private bool CancelCondition(ITrade trade) =>
      autoStop && (trade.Price > highPrice || lowPrice > trade.Price);

    private void Sell(IOrder buyOrder, IEnumerable<Grid> hits) {

      var price = buyOrder.ExecutedPrice * (1m + sellThreshold);
      var quantity = buyOrder.Quantity;

      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] SELL (price = {price})");

      IOrder order = market
        .Sell(quantity)
        .For(price)
        .OnExecuted(o => Sold(o, hits))
        .OnCancel(o => EmergencySell(hits))
        .CancelIf((o, t) => CancelCondition(t))
        .Submit();

      foreach (var hit in hits)
        hit.SellOrder = order;
    }

    private void EmergencySell(IEnumerable<Grid> grids) {
      if(autoStop) {
        var qty = grids.Sum(x => x.SellOrder?.ExecutedAmount ?? 0m);
        market
          .Sell(qty)
          .OnExecuted(o => Sold(o, grids))
          .Submit();
      }
    }

    private void Sold(IOrder sellOrder, IEnumerable<Grid> hits) {
      Profit += sellOrder.ExecutedAmount;
      foreach (var hit in hits)
        hit.SellOrder = null;
      Log.Info($"SOLD [{sellOrder}] Profit = {Profit}");
    }

    private void PrintGrids() {
      foreach (var grid in grids)
        Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] {grid}");
    }
  }
}
