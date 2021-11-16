using PiTrade.Exchange.Delegates;
using PiTrade.Exchange.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange
{
  public interface IMarket
  {
    IExchange Exchange { get; }
    Symbol Asset { get; }
    Symbol Quote { get; }
    int AssetPrecision { get; }
    int QuotePrecision { get; }
    IEnumerable<Order> ActiveOrders { get; }
    Task<Order> Buy(decimal price, decimal quantity);
    Task<Order> Sell(decimal price, decimal quantity);
    Task Cancel(Order order);
    Task CancelAll();
    Task Listen(
      Func<Order, Task> onBuy, 
      Func<Order, Task> onSell,
      Func<decimal, Task> onPriceUpdate,
      CancellationToken token);
  }
}
