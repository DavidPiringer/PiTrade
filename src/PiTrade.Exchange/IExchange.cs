using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  public interface IExchange {
    Task<IMarket[]> GetMarkets();
    Task<IMarket?> GetMarket(Symbol asset, Symbol quote);

    Task Subscribe(params IMarket[] markets);
    Task Run(CancellationToken cancellationToken);


    Task<long> CreateMarketOrder(
      IMarket market, OrderSide side, decimal quantity,
      Action<IOrder, ITrade> onTrade, Action<IOrder> onCancel, Action<IOrder> onError);

    Task<long> CreateLimitOrder(
      IMarket market, OrderSide side, decimal quantity, decimal price,
      Action<IOrder, ITrade> onTrade, Action<IOrder> onCancel, Action<IOrder> onError);

    Task<bool> CancelOrder(IMarket market, long orderId);

  }
}
