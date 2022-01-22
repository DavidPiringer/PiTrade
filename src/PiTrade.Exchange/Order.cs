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
    private readonly IList<Action<Order>> whenFilledActions = new List<Action<Order>>();

    public event Action<Order>? FillExecuted;

    public long Id { get; private set; }
    public Market Market { get; }
    public OrderSide Side { get; }
    public decimal TargetPrice { get; }
    public decimal Quantity { get; }
    public decimal Amount { get; private set; }
    public decimal ExecutedAmount { get; private set; }
    public decimal ExecutedQuantity { get; private set; }
    public decimal AvgFillPrice { get; private set; }
    public bool IsFilled { get; private set; }
    public bool IsCancelled { get; private set; }

    public Order(Task<(long? orderId, ErrorState error)> creationTask, 
      Market market, OrderSide side, decimal targetPrice, decimal quantity) {
      Market = market;
      Side = side;
      TargetPrice = targetPrice;
      Quantity = quantity;
      market.TradeUpdate += OnTradeUpdate;

      creationTask.ContinueWith(Creation);
    }

    private void Creation(Task<(long? orderId, ErrorState error)> creationTask) {
      creationTask.Wait();
      (long? orderId, ErrorState error) = creationTask.Result;
      if(error == ErrorState.None && orderId.HasValue)
        Id = orderId.Value;
    }

    private void OnTradeUpdate(IMarket market, ITradeUpdate update) {
      if (update.Match(this)) {
        ExecutedQuantity += update.Quantity;
        AvgFillPrice += update.Price * (update.Quantity / Quantity);
        ExecutedAmount = AvgFillPrice * ExecutedQuantity;
        Amount = AvgFillPrice * ExecutedQuantity;
        IsFilled = Quantity <= ExecutedQuantity;
        FillExecuted?.Invoke(this);
        if (IsFilled) ExecuteWhenFilledActions();
      }
    }

    private void ExecuteWhenFilledActions() {
      foreach(var fnc in whenFilledActions)
        fnc(this);
    }

    public void Cancel() {
      if (!IsFilled && !IsCancelled) {
        IsCancelled = true;
        _ = ExponentialBackoff.Try(async () => ErrorState.ConnectionLost == await Market.CancelOrder(this));
      }
    }

    public void WhenFilled(Action<Order> fnc) =>
      whenFilledActions.Add(fnc);

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
        Cancel();
        disposedValue = true;
      }
    }

    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    ~Order() => Dispose(disposing: false);

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
