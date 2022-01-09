using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public class Order : IDisposable {
    private readonly object locker = new object();
    private readonly TaskCompletionSource fillTCS = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource CTS = new CancellationTokenSource();

    private decimal summedPriceFills = 0m;
    private int fillCount = 0;

    public long Id { get; }
    public Market Market { get; }
    public OrderSide Side { get; }
    public decimal TargetPrice { get; }
    public decimal Quantity { get; }
    public decimal Amount { get; private set; }
    public decimal ExecutedAmount { get; private set; }
    public decimal ExecutedQuantity { get; private set; }
    public decimal AvgFillPrice { get; private set; }
    public bool IsFilled { get; private set; }
    public bool IsCancelled => CTS.IsCancellationRequested;


    public Order(long id, Market market, OrderSide side, decimal targetPrice, decimal quantity) {
      Id = id;
      Market = market;
      Side = side;
      TargetPrice = targetPrice;
      Quantity = quantity;
      market.RegisterOrder(this);
    }


    internal void Update(ITradeUpdate update) {
      if (update.Match(this)) {
        // set all property values in a locked environment
        // set the values instead of calculating them in their getter
        // -> prevents race conditions
        lock (locker) {
          ExecutedQuantity += update.Quantity;
          summedPriceFills += update.Price;
          fillCount++;
          AvgFillPrice = summedPriceFills / fillCount;
          ExecutedAmount = AvgFillPrice * ExecutedQuantity;
          Amount = AvgFillPrice * Quantity;
          IsFilled = Quantity <= ExecutedQuantity;
          if (IsFilled) fillTCS.SetResult();
        }
      }
    }

    public async Task Cancel() {
      if (!IsFilled && !CTS.IsCancellationRequested) {
        await ExponentialBackoff.Try(async () => ErrorState.ConnectionLost == await Market.CancelOrder(this));
        CTS.Cancel();
      }
    }

    /// <summary>
    /// Starts a new long running task, which waits for the full execution of the order.
    /// After the order is filled, it continues with a defined function.
    /// </summary>
    public void WhenFilled(Action<Order> fnc) =>
      WhenFilled(o => Task.Run(() => fnc(o)));

    /// <summary>
    /// Starts a new long running task, which waits for the full execution of the order.
    /// After the order is filled, it continues with a defined function.
    /// </summary>
    public void WhenFilled(Func<Order, Task> fnc) =>
      Task.Factory.StartNew(async () => {
        await fillTCS.Task;
        if(!CTS.Token.IsCancellationRequested)
          await fnc.Invoke(this);
      }, CTS.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public override string ToString() =>
      $"Id = {Id}, " +
      $"Market = {Market}, " +
      $"Side = {Side}, " +
      $"Price = {TargetPrice}, " +
      $"Quantity = {Quantity}, " +
      $"ExecutedQuantity = {ExecutedQuantity}, " +
      $"Amount = {TargetPrice * Quantity}";


    #region Disposable Support
    private bool disposedValue = false;
    protected virtual void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
          // TODO: dispose managed state (managed objects)
        }

        Cancel().Wait();
        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    ~Order() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: false);
    }

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
