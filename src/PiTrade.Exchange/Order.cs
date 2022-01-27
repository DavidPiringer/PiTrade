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
  public abstract class Order : IOrder {
    private readonly IExchangeAPI api;
    private readonly IList<Func<Order, Task>> whenFilledActions = new List<Func<Order, Task>>();
    private readonly IList<Func<Order, Task>> whenFaultedActions = new List<Func<Order, Task>>();

    public long? Id { get; private set; }
    public IMarket Market { get; }
    public OrderSide Side { get; }
    public decimal TargetPrice { get; }
    public decimal Quantity { get; }
    public decimal Amount { get; private set; }
    public decimal ExecutedAmount { get; protected set; }
    public decimal ExecutedQuantity { get; protected set; }
    public decimal AvgFillPrice { get; protected set; }
    public bool IsFilled { get; protected set; }
    public bool IsCancelled { get; protected set; }
    public bool IsFaulted { get; protected set; }

    internal Order(IExchangeAPI api, long? orderId, IMarket market, OrderSide side, decimal targetPrice, decimal quantity) {
      this.api = api;
      Id = orderId;
      Market = market;
      Side = side;
      TargetPrice = targetPrice;
      Quantity = quantity;
      market.Register2TradeUpdates(OnTradeUpdate);
    }

    internal async Task OnTradeUpdate(IMarket market, ITradeUpdate update) {
      if (Id.HasValue && update.Match(Id.Value)) {
        ExecutedQuantity += update.Quantity;
        AvgFillPrice += update.Price * (update.Quantity / Quantity);
        ExecutedAmount = AvgFillPrice * ExecutedQuantity;
        Amount = AvgFillPrice * ExecutedQuantity;
        IsFilled = Quantity <= ExecutedQuantity;
        if (IsFilled) {
          await ExecuteWhenFilledActions();
          market.Unregister2TradeUpdates(OnTradeUpdate);
        }
      }
    }

    private async Task ExecuteWhenFilledActions() {
      foreach(var fnc in whenFilledActions)
        await fnc(this);
      whenFilledActions.Clear();
    }

    public async Task Cancel() {
      if (!IsFilled && !IsCancelled) {
        IsCancelled = true;
        Market.Unregister2TradeUpdates(OnTradeUpdate);
        await ExponentialBackoff.Try(async () => ErrorState.ConnectionLost == await Market.CancelOrder(this));
      }
    }

    public async Task WhenFilled(Action<Order> fnc) => 
      await WhenFilled(async o => {
        fnc(o);
        await Task.CompletedTask;
      });

    public async Task WhenFilled(Func<Order, Task> fnc) {
      if (IsFilled) await fnc(this);
      else whenFilledActions.Add(fnc);
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
        Log.Info($"Dispose Order [{GetHashCode()}][{ToString()}]");
        Cancel().Wait(5000);
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
