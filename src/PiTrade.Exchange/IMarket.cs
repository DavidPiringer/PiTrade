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
    IExchange Exchange { get; }
    Symbol QuoteAsset { get; }
    Symbol BaseAsset { get; }
    int QuoteAssetPrecision { get; }
    int BaseAssetPrecision { get; }

    IOrder Sell(decimal quantity);
    IOrder Buy(decimal quantity);

    void Subscribe(Action<ITrade> onTrade);
    void Unsubscribe(Action<ITrade> onTrade);
  }
}
