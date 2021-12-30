using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public class Order : IDisposable {
    private readonly object locker = new object();
    private readonly TaskCompletionSource fillTCS = new TaskCompletionSource();

    private decimal summedPriceFills = 0m;
    private int fillCount = 0;

    public long Id { get; }
    public Market Market { get; }
    public OrderSide Side { get; }
    public decimal TargetPrice { get; }
    public decimal Quantity { get; }
    public decimal Amount => AvgFillPrice * Quantity;
    public decimal ExecutedAmount => AvgFillPrice * ExecutedQuantity;
    public decimal ExecutedQuantity { get; private set; }
    public bool IsFilled => Quantity <= ExecutedQuantity;
    public bool IsCancelled { get; private set; }
    public decimal AvgFillPrice {
      get {
        lock (locker) { return summedPriceFills / fillCount; }
      }
    }


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
        lock (locker) {
          ExecutedQuantity += update.Quantity;
          summedPriceFills += update.Price;
          fillCount++;
          if (IsFilled) fillTCS.SetResult();
        }
      }
    }

    public async Task Cancel() {
      if (!IsFilled && !IsCancelled) {
        await ExponentialBackoff.Try(async () => ErrorType.ConnectionLost == await Market.CancelOrder(this));
        IsCancelled = true;
      }
    }

    public async Task WhenFilled(Func<Order, Task> fnc) {
      await fillTCS.Task;
      await fnc.Invoke(this);
    }

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
