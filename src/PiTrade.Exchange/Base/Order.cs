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
  public sealed class Order : IOrder {
    private readonly IExchangeAPIClient api;
    private readonly IList<Func<Order, Task>> whenFilledActions = new List<Func<Order, Task>>();
    private readonly IList<Func<Order, Task>> whenCanceledActions = new List<Func<Order, Task>>();
    private readonly IList<Func<Order, Task>> whenFaultedActions = new List<Func<Order, Task>>();
    private readonly TimeSpan updateInterval = TimeSpan.FromMinutes(2.0);

    public long Id { get; private set; }
    public IMarket Market { get; private set; }
    public OrderSide Side { get; private set; }
    public decimal TargetPrice { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Amount => Quantity * TargetPrice;
    public decimal ExecutedAmount => ExecutedQuantity * AvgFillPrice;
    public decimal ExecutedQuantity { get; private set; }
    public decimal AvgFillPrice { get; private set; }
    public OrderState State { get; private set; }

    private DateTime lastUpdate = DateTime.Now;

    private Order(IExchangeAPIClient api, IMarket market) {
      this.api = api;
      Market = market;
    }

    internal static async Task<Order> Create(IExchangeAPIClient api, IMarket market, Func<Task<OrderDTO?>> creationFnc) {
      var order = new Order(api, market);
      market.Register2TradeUpdates(order.OnTradeUpdate);
      var dto = await creationFnc();
      order.Init(market, dto);
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
      if (lastUpdate.Add(updateInterval) < DateTime.Now) { 
        lastUpdate = DateTime.Now;
        await UpdateSelf();
      }
      if (State == OrderState.Open && update.Match(Id)) {
        ExecutedQuantity += update.Quantity;
        AvgFillPrice += update.Price * (update.Quantity / Quantity);
        if (Quantity <= ExecutedQuantity) {
          State = OrderState.Filled;
          await ExecuteWhenActions(whenFilledActions);
        }
      }
    }

    private async Task UpdateSelf() {
      var dto = await api.GetOrder(Market, Id);
      if (dto.HasValue) {
        ExecutedQuantity = dto.Value.ExecutedQuantity;
        AvgFillPrice = dto.Value.AvgFillPrice;
        State = dto.Value.State;
        if (State == OrderState.Filled)
          await ExecuteWhenActions(whenFilledActions);
        else if(State == OrderState.Canceled)
          await ExecuteWhenActions(whenCanceledActions);
      }
    }

    private async Task ExecuteWhenActions(IList<Func<Order, Task>> list) {
      Market.Unregister2TradeUpdates(OnTradeUpdate);
      foreach (var fnc in list)
        await fnc(this);
      list.Clear();
    }

    public async Task Cancel() {
      if (State != OrderState.Filled && State != OrderState.Canceled) {
        State = OrderState.Canceled;
        await ExponentialBackoff.Try(async () => !await api.CancelOrder(Market, Id), attempts: 5);
        await ExecuteWhenActions(whenCanceledActions);
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

    public async Task WhenCanceled(Action<IOrder> fnc) =>
      await WhenCanceled(async o => {
        fnc(o);
        await Task.CompletedTask;
      });

    public async Task WhenCanceled(Func<IOrder, Task> fnc) {
      if (State == OrderState.Canceled) await fnc(this);
      else whenCanceledActions.Add(fnc);
    }

    public override string ToString() =>
      //$"Id = {Id}, " +
      $"Market = {Market}, " +
      $"Side = {Side}, " +
      $"Price = {TargetPrice}({AvgFillPrice}), " +
      $"Quantity = {ExecutedQuantity}/{Quantity}";


    #region Disposable Support
    private bool disposedValue = false;
    private void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
          // TODO: dispose managed state (managed objects)
        }
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
