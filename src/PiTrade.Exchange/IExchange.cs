using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  public interface IExchange {
    IEnumerable<IMarket> Markets { get; }

    Task<long> CreateMarketOrder(
      IMarket market, OrderSide side, decimal quantity);

    Task<long> CreateLimitOrder(
      IMarket market, OrderSide side, decimal quantity, decimal price);

    Task<bool> CancelOrder(IMarket market, long orderId);

    void Subscribe(IMarket market, Action<ITrade> onTrade);
    void Unsubscribe(IMarket market, Action<ITrade> onTrade);

  }
}
