using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  public interface IOrder : IDisposable {

    long Id { get; }
    IMarket Market { get; }
    OrderType Type { get; }
    OrderSide Side { get; }
    decimal Price { get; }
    decimal Quantity { get; }
    decimal Amount { get; }
    decimal ExecutedPrice { get; }
    decimal ExecutedQuantity { get; }
    decimal ExecutedAmount { get; }
    IEnumerable<ITrade> Trades { get; }
    OrderState State { get; }


    IOrder For(decimal price);
    IOrder OnTrade(Action<IOrder, ITrade> fnc);
    IOrder OnCancel(Action<IOrder> fnc);
    IOrder OnError(Action<IOrder> action);

    Task<IOrder> Transmit();
    Task Cancel();

  }
}
