using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.DTOs;
using PiTrade.Exchange.Enums;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public interface IExchangeAPIClient {
    string Name { get; }
    decimal CommissionFee { get; }
    Task<MarketDTO[]> FetchMarkets();
    Task<OrderDTO?> CreateMarketOrder(IMarket market, OrderSide side, decimal quantity);
    Task<OrderDTO?> CreateLimitOrder(IMarket market, OrderSide side, decimal price, decimal quantity);
    Task<OrderDTO?> GetOrder(IMarket market, long orderId);
    Task<bool> CancelOrder(IOrder order);
    Task<bool> CancelOrder(IMarket market, long orderId);

  }
}
