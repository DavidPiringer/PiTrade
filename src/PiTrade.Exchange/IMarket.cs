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

    decimal CurrentPrice { get; }

    IExchange Exchange { get; }

    Symbol Asset { get; }

    Symbol Quote { get; }

    int AssetPrecision { get; }

    int QuotePrecision { get; }

    IEnumerable<IIndicator> Indicators { get; }

    /// <summary>
    /// Connects async to the corresponding exchange endpoint.
    /// </summary>
    /// <returns>Returnsa long running task which runs the market loop.</returns>
    Task Connect();

    // TODO: Disconnect?

    void AddIndicator(IIndicator indicator);

    void Listen(Func<decimal, Task> fnc);

    Task<(Order? order, ErrorState error)> CreateMarketOrder(OrderSide side, decimal quantity);

    Task<(Order? order, ErrorState error)> CreateLimitOrder(OrderSide side, decimal price, decimal quantity);

    //IMarketHandle GetMarketHandle(out Task awaitTask, IOrderListener? listener = null);
  }
}
