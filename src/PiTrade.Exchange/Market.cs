using System;
using System.Collections.Concurrent;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Exchange.Util;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public abstract class Market : IMarket {
    private readonly object locker = new object();
    private readonly IDictionary<long, PriceCandleTicker> priceCandleTickers = new Dictionary<long, PriceCandleTicker>();
    private readonly IList<IIndicator> indicators = new List<IIndicator>();

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public IEnumerable<Order> ActiveOrders => orders.Values;
    public IEnumerable<IIndicator> Indicators => indicators.ToArray();

    public decimal CurrentPrice {
      get => currentPrice;
      protected set {
        lock(locker) {
          OnPriceUpdate(value);
          currentPrice = value;
        }
      }
    }



    //TODO: listen -> return MarketHandle, add indicators (MA50, ..)
    private ConcurrentDictionary<long, Order> orders = new ConcurrentDictionary<long, Order>();

    private decimal currentPrice;

    public Market(IExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      Exchange = exchange;
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
    }

    public void AddIndicator(IIndicator indicator) {
      var key = (long)indicator.Period.TotalMilliseconds;
      PriceCandleTicker? ticker;
      if(!priceCandleTickers.TryGetValue(key, out ticker)) {
        ticker = new PriceCandleTicker(indicator.Period);
        priceCandleTickers.Add(key, ticker);
      }
      if(ticker != null) {
        ticker.Tick += indicator.Update;
        indicators.Add(indicator);
      }
    }


    public async Task<Order> Buy(decimal price, decimal quantity) {
      var order = await NewOrder(OrderSide.BUY,
        price.RoundDown(QuotePrecision),
        quantity.RoundUp(AssetPrecision));
      orders.TryAdd(order.Id, order);
      return order;
    }

    public async Task<Order> Sell(decimal price, decimal quantity) {
      var order = await NewOrder(OrderSide.SELL,
        price.RoundUp(QuotePrecision),
        quantity.RoundDown(AssetPrecision));
      orders.TryAdd(order.Id, order);
      return order;
    }

    public virtual async Task Cancel(Order order) =>
      await Task.Run(() => orders.TryRemove(order.Id, out Order? tmp));

    public virtual async Task CancelAll() =>
      await Task.Run(() => orders.Clear());


    public abstract Task<Order> NewOrder(OrderSide side, decimal price, decimal quantity);
    public abstract Task Listen(Func<Order, Task> onBuy, Func<Order, Task> onSell, Func<decimal, Task> onPriceUpdate, CancellationToken token);

    public override string ToString() => $"{Asset}{Quote}";


    private void OnPriceUpdate(decimal price) {
      foreach (var ticker in priceCandleTickers.Values)
        ticker.PriceUpdate(price);
    }
  }
}
