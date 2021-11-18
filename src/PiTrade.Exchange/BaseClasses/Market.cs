using System;
using System.Collections.Concurrent;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Extensions;
using PiTrade.Networking;

namespace PiTrade.Exchange.BasesClasses {
  public abstract class Market : IMarket {
    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }


    private ConcurrentDictionary<long, Order> orders = new ConcurrentDictionary<long, Order>();
    public IEnumerable<Order> ActiveOrders => orders.Values;


    public Market(IExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      Exchange = exchange;
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
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
  }
}
