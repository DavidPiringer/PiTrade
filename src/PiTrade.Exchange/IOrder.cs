﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange {
  /// <summary>
  /// Represent the interface for all order implementations.
  /// </summary>
  public interface IOrder : IDisposable {

    /// <summary>
    /// Order ID
    /// </summary>
    long Id { get; }
    /// <summary>
    /// referenced market of the order
    /// </summary>
    IMarket Market { get; }
    /// <summary>
    /// order type (Market, Limit, ...)
    /// </summary>
    OrderType Type { get; }
    /// <summary>
    /// BUY/SELL Side
    /// </summary>
    OrderSide Side { get; }
    /// <summary>
    /// state for the order (Open, Canceled, Filled, ...)
    /// </summary>
    OrderState State { get; }
    /// <summary>
    /// targeted price for the order
    /// </summary>
    decimal Price { get; }
    /// <summary>
    /// required quantity of the quote asset
    /// </summary>
    decimal Quantity { get; }
    /// <summary>
    /// QoL property for Price * Quantity
    /// </summary>
    decimal Amount { get; }
    /// <summary>
    /// the executed price (can deviate from Price)
    /// </summary>
    decimal ExecutedPrice { get; }
    /// <summary>
    /// the executed quantity (can deviate from Quantity due to commission fees)
    /// </summary>
    decimal ExecutedQuantity { get; }
    /// <summary>
    /// QoL property for ExecutedPrice * ExecutedQuantity
    /// </summary>
    decimal ExecutedAmount { get; }
    /// <summary>
    /// array of all trades referencing this order
    /// </summary>
    IEnumerable<ITrade> Trades { get; }

    /// <summary>
    /// Set a targeted price for this order. The order type changes to "Limit".
    /// </summary>
    /// <param name="price">Limit Order Price</param>
    /// <returns>similar order instance</returns>
    IOrder For(decimal price);

    /// <summary>
    /// Set a callback to fire after the order was executed.
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnExecuted(Action<IOrder> fnc);
    /// <summary>
    /// async version of OnExecuted
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnExecutedAsync(Func<IOrder, Task> fnc);

    /// <summary>
    /// Set a callback to fire on each trade event in which the order is involved.
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnTrade(Action<IOrder, ITrade> fnc);
    /// <summary>
    /// async version of OnTrade
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnTradeAsync(Func<IOrder, ITrade, Task> fnc);

    /// <summary>
    /// Set a callback to fire when the order is getting canceled.
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnCancel(Action<IOrder> fnc);
    /// <summary>
    /// async version of OnCancel
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnCancelAsync(Func<IOrder, Task> fnc);

    /// <summary>
    /// Set a callback to fire after an error occurred.
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnError(Action<IOrder, Exception> action);
    /// <summary>
    /// async version of OnError
    /// </summary>
    /// <param name="fnc">Callback action</param>
    /// <returns>similar order instance</returns>
    IOrder OnErrorAsync(Func<IOrder, Exception, Task> fnc);

    /// <summary>
    /// Submits the order to the exchange.
    /// </summary>
    /// <returns> similar order instance</returns>
    IOrder Submit();

    /// <summary>
    /// Submits the order to the exchange asynchronously.
    /// </summary>
    /// <returns>similar order instance</returns>

    Task<IOrder> SubmitAsync();

    /// <summary>
    /// Cancels the order.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Cancels the order asynchronously.
    /// </summary>
    Task CancelAsync();

    /// <summary>
    /// Cancels the order after a time span has past.
    /// </summary>
    /// <returns>similar order instance</returns>
    IOrder CancelAfter(TimeSpan timeSpan);

    /// <summary>
    /// Cancels the order if the predicate is true. 
    /// The predicate is called for every trade on the market.
    /// </summary>
    /// <param name="predicate">Callback to check if order should be canceled</param>
    /// <returns>similar order instance</returns>
    IOrder CancelIf(Func<IOrder, ITrade, bool> predicate);
  }
}
