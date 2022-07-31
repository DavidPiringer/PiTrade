using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  public struct OrderCreationResult {
    public long OrderId { get; set; }
    public IEnumerable<ITrade> MatchedOrders { get; set; }
  }

  public interface IExchange {
    decimal MinimalAmountPerOrder { get; }
    IEnumerable<IMarket> Markets { get; }

    Task<OrderCreationResult> CreateMarketOrder(
      IMarket market, OrderSide side, decimal quantity);

    Task<OrderCreationResult> CreateLimitOrder(
      IMarket market, OrderSide side, decimal quantity, decimal price);

    Task<bool> CancelOrder(IMarket market, long orderId);

    Task<IEnumerable<PriceCandle>> GetMarketData(
      IMarket market, PriceCandleInterval interval, uint limit);

    void Subscribe(IMarket market, Action<ITrade> onTrade);
    void Unsubscribe(IMarket market, Action<ITrade> onTrade);

  }
}
