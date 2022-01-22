using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange {
  public interface IMarket {
    event Action<IMarket, ITradeUpdate>? TradeUpdate;
    event Action<IMarket, decimal>? PriceChanged;

    decimal CurrentPrice { get; }
    IExchange Exchange { get; }
    Symbol Asset { get; }
    Symbol Quote { get; }
    int AssetPrecision { get; }
    int QuotePrecision { get; }
    Order CreateMarketOrder(OrderSide side, decimal quantity);
    Order CreateLimitOrder(OrderSide side, decimal price, decimal quantity);

  }
}
