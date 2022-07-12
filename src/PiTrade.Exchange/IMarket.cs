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
    //decimal CurrentPrice { get; }
    IExchange Exchange { get; }
    Symbol QuoteAsset { get; }
    Symbol BaseAsset { get; }
    int QuoteAssetPrecision { get; }
    int BaseAssetPrecision { get; }

    IOrder Sell(decimal quantity);
    IOrder Buy(decimal quantity);

    //Task<IOrder> CreateMarketOrder(OrderSide side, decimal quantity);
    //Task<IOrder> CreateLimitOrder(OrderSide side, decimal price, decimal quantity);

    //void Register2TradeUpdates(Func<IMarket, ITradeUpdate, Task> fnc);
    //void Unregister2TradeUpdates(Func<IMarket, ITradeUpdate, Task> fnc);

    //void Register2PriceChanges(Func<IMarket, decimal, Task> fnc);
    //void Unregister2PriceChanges(Func<IMarket, decimal, Task> fnc);

  }
}
