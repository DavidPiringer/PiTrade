using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;

namespace PiTrade.Exchange {
  public class MarketHandle : IMarketHandle {
    private readonly Market market;
    private readonly IOrderListener? listener;
    private readonly ConcurrentDictionary<long, Order> orders = new ConcurrentDictionary<long, Order>();

    public IEnumerable<Order> ActiveOrders => orders.Values;

    internal MarketHandle(Market market, IOrderListener? listener = null) {
      this.market = market;
      this.listener = listener;
    }

    public async Task<Order> Buy(decimal price, decimal quantity) {
      var order = await market.NewOrder(OrderSide.BUY, 
        price.RoundDown(market.QuotePrecision), 
        quantity.RoundUp(market.AssetPrecision));
      orders.TryAdd(order.Id, order);
      return order;
    }

    public async Task<Order> Sell(decimal price, decimal quantity) {
      var order = await market.NewOrder(OrderSide.SELL, 
        price.RoundUp(market.QuotePrecision), 
        quantity.RoundDown(market.AssetPrecision));
      orders.TryAdd(order.Id, order);
      return order;
    }

    public async Task Cancel(Order order) {
      orders.TryRemove(order.Id, out Order? tmp);
      await market.CancelOrder(order);
    }

    public async Task CancelAll() {
      foreach(var order in ActiveOrders.Where(x => !x.IsFilled))
        await Cancel(order);
    }

    public void Dispose() => market.RemoveMarketHandle(this);

    internal async void Update(ITradeUpdate update) {
      if (listener != null)
        await listener.OnPriceUpdate(update.Price);

      var matchedOrder = ActiveOrders.Where(x => update.Match(x)).FirstOrDefault();
      if (matchedOrder != null) {
        matchedOrder.Fill(update.Quantity, update.Price);
        if (listener != null) {
          await (matchedOrder.Side switch {
            OrderSide.BUY => listener.OnBuy(matchedOrder),
            OrderSide.SELL => listener.OnSell(matchedOrder),
            _ => Task.CompletedTask
          });
        }

        if(matchedOrder.IsFilled) // cleanup filled orders
          orders.TryRemove(matchedOrder.Id, out Order? tmp);
      }
    }
  }
}
