using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public interface IMarketHandle {
    IEnumerable<Order> ActiveOrders { get; }
    Task<Order> Buy(decimal price, decimal quantity);
    Task<Order> Sell(decimal price, decimal quantity);
    Task Cancel(Order order);
    Task CancelAll();
  }
}
