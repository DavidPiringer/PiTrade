using PiTrade.Exchange.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange
{
  public abstract class Exchange : IExchange
  {
    public IEnumerable<Order> ActiveOrders => throw new NotImplementedException();
    public IEnumerable<Market> AvailableMarkets => throw new NotImplementedException();


    public abstract Task<Order> Get(int id);

    public async Task<Order> Buy(Market market, decimal price, decimal quantity)
    {
      return await NewOrder(OrderType.BUY, market, price, quantity);
    }

    public async Task<Order> Sell(Market market, decimal price, decimal quantity)
    {
      return await NewOrder(OrderType.SELL, market, price, quantity);
    }

    public abstract Task Cancel(Order order);

    public abstract Task CancelAll();

    protected abstract Task<Order> NewOrder(OrderType type, Market market, decimal price, decimal quantity);
  }
}
