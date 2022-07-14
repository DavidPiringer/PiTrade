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
    /// <summary>
    /// the referenced exchange
    /// </summary>
    IExchange Exchange { get; }
    /// <summary>
    /// the Asset to Buy/Sell
    /// </summary>
    Symbol QuoteAsset { get; }
    /// <summary>
    /// the Asset to pay/collect with
    /// </summary>
    Symbol BaseAsset { get; }
    int QuoteAssetPrecision { get; }
    int BaseAssetPrecision { get; }

    /// <summary>
    /// creates a market sell order (can be upgrade to Limit with IOrder API)
    /// </summary>
    /// <param name="quantity">the quantity of the quote asset</param>
    /// <returns>order instance</returns>
    IOrder Sell(decimal quantity);
    /// <summary>
    /// creates a market buy order (can be upgrade to Limit with IOrder API)
    /// </summary>
    /// <param name="quantity">the quantity of the quote asset</param>
    /// <returns>order instance</returns>
    IOrder Buy(decimal quantity);

    void Subscribe(Action<ITrade> onTrade);
    void SubscribeAsync(Func<ITrade,Task> onTrade);

    void Unsubscribe(Action<ITrade> onTrade);
    void UnsubscribeAsync(Func<ITrade, Task> onTrade);
  }
}
