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
    OrderSide Side { get; }
    decimal TargetPrice { get; }
    decimal Quantity { get; }
    decimal Amount { get; }
    decimal ExecutedAmount { get; }
    decimal ExecutedQuantity { get; }
    decimal AvgFillPrice { get; }
    OrderState State { get; }

    Task Cancel();
    Task WhenFilled(Action<IOrder> fnc);
    Task WhenFilled(Func<IOrder, Task> fnc);
    Task WhenCanceled(Action<IOrder> fnc);
    Task WhenCanceled(Func<IOrder, Task> fnc);
  }
}
