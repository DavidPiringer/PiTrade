using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  public interface IMarketHandle : IDisposable {
    IEnumerable<Order> ActiveOrders { get; }
    Task<Order> Market(OrderSide side, decimal quantity);
    Task<Order> BuyLimit(decimal price, decimal quantity);
    Task<Order> SellLimit(decimal price, decimal quantity);
    Task<Order> StopLoss(OrderSide side, decimal stopPrice, decimal quantity);
    //Task<Order> StopLimit(OrderSide side, decimal stopPrice, decimal price, decimal quantity);
    Task Cancel(Order order);
    Task CancelAll();
  }
}
