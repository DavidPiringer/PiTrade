using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.DTOs;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;
using PiTrade.Networking;

namespace PiTrade.Exchange.Base {
  public class Order : IOrder {
    private readonly IExchangeAPIClient api;
    private readonly IList<Func<Order, Task>> whenFilledActions = new List<Func<Order, Task>>();
    private readonly IList<Func<Order, Task>> whenFaultedActions = new List<Func<Order, Task>>();

    public long Id { get; private set; }
    public IMarket Market { get; private set; }
    public OrderSide Side { get; private set; }
    public decimal TargetPrice { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Amount { get; private set; }
    public decimal ExecutedAmount { get; private set; }
    public decimal ExecutedQuantity { get; private set; }
    public decimal AvgFillPrice { get; private set; }
    public OrderState State { get; private set; }

    private Order(IExchangeAPIClient api, IMarket market) {
      this.api = api;
      Market = market;
    }

    internal static async Task<Order> Create(IExchangeAPIClient api, IMarket market, Func<Task<OrderDTO?>> creationFnc) {
      var order = new Order(api, market);
      market.Register2TradeUpdates(order.OnTradeUpdate);
      var dto = await creationFnc();
      order.Init(market, dto.Value);
      return order;
    }

    private void Init(IMarket market, OrderDTO? dto) {
      Market = market;
      if(dto.HasValue) {
        Id = dto.Value.Id;
        Side = dto.Value.Side;
        TargetPrice = dto.Value.TargetPrice;
        Quantity = dto.Value.Quantity;
        State = OrderState.Open;
      } else {
        State = OrderState.Faulted;
      }
    }

    internal async Task OnTradeUpdate(IMarket market, ITradeUpdate update) {
      if (State == OrderState.Open && update.Match(Id)) {
        ExecutedQuantity += update.Quantity;
        AvgFillPrice += update.Price * (update.Quantity / Quantity);
        ExecutedAmount = AvgFillPrice * ExecutedQuantity;
        Amount = AvgFillPrice * ExecutedQuantity;
        if (Quantity <= ExecutedQuantity) {
          State = OrderState.Filled;
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
      if (State != OrderState.Filled && State != OrderState.Cancelled) {
        State = OrderState.Cancelled;
        Market.Unregister2TradeUpdates(OnTradeUpdate);
        await ExponentialBackoff.Try(async () => await api.CancelOrder(Id));
      }
    }

    public async Task WhenFilled(Action<IOrder> fnc) => 
      await WhenFilled(async o => {
        fnc(o);
        await Task.CompletedTask;
      });

    public async Task WhenFilled(Func<IOrder, Task> fnc) {
      if (State == OrderState.Filled) await fnc(this);
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
