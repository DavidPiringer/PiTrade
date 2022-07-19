using System.Linq;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;

namespace PiTrade.Exchange.Base {
  public sealed class Order : IOrder {
    private readonly IList<ITrade> tmpTrades = new List<ITrade>();
    private readonly IList<ITrade> trades = new List<ITrade>();
    private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    public long Id { get; private set; }
    public IMarket Market { get; private set; }
    public OrderType Type { get; private set; }
    public OrderSide Side { get; private set; }
    public OrderState State { get; private set; }
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Amount => Quantity * Price;
    public decimal ExecutedPrice => Trades.Count() > 0 ? Trades.Average(x => x.Price) : 0;
    public decimal ExecutedQuantity => Trades.Count() > 0 ? Trades.Sum(x => x.Quantity) : 0;
    public decimal ExecutedAmount => ExecutedQuantity * ExecutedPrice;
    public IEnumerable<ITrade> Trades => trades;


    private Action<IOrder> onExecuted;
    private Action<IOrder, ITrade> onTrade;
    private Action<IOrder> onCancel;
    private Action<IOrder, Exception> onError;
    private Func<IOrder, ITrade, bool> cancelIfPredicate;

    internal Order(IMarket market, OrderSide side, decimal quantity) {
      Id = -1;
      Market = market;
      Type = OrderType.Market;
      Side = side;
      Quantity = Side switch {
        OrderSide.BUY => quantity.RoundUp(Market.BaseAssetPrecision),
        OrderSide.SELL => quantity.RoundDown(Market.BaseAssetPrecision),
        _ => throw new NotImplementedException()
      };
      onExecuted = (o) => OnExecutedWrapper(o);
      onTrade = (o, t) => OnTradeWrapper(o, t);
      onCancel = (o) => OnCancelWrapper(o);
      onError = (o, err) => OnErrorWrapper(o, err);
      cancelIfPredicate = (o,t) => false;
    }

    public override string ToString() =>
      $"Id = {Id}, " +
      $"Market = {Market}, " +
      $"Side = {Side}, " +
      $"Type = {Type}, " +
      $"Price = {Price}({ExecutedPrice}), " +
      $"Quantity = {ExecutedQuantity}/{Quantity}";

    public IOrder For(decimal price) {
      Type = OrderType.Limit;
      Price = Side switch {
        OrderSide.BUY => price.RoundDown(Market.QuoteAssetPrecision),
        OrderSide.SELL => price.RoundUp(Market.QuoteAssetPrecision),
        _ => throw new NotImplementedException()
      };
      return this;
    }

    public IOrder OnExecuted(Action<IOrder> fnc) {
      onExecuted = (o) => OnExecutedWrapper(o, fnc);
      return this;
    }
    public IOrder OnExecutedAsync(Func<IOrder, Task> fnc) =>
      OnExecuted((o) => { Task.Run(async () => await fnc(o)); });


    public IOrder OnTrade(Action<IOrder, ITrade> fnc) {
      onTrade = (o, t) => OnTradeWrapper(o, t, fnc);
      return this;
    }
    public IOrder OnTradeAsync(Func<IOrder, ITrade, Task> fnc) =>
      OnTrade((o,t) => { Task.Run(async () => await fnc(o,t)); });

    public IOrder OnCancel(Action<IOrder> fnc) {
      onCancel = (o) => OnCancelWrapper(o, fnc);
      return this;
    }
    public IOrder OnCancelAsync(Func<IOrder, Task> fnc) =>
      OnCancel((o) => { Task.Run(async () => await fnc(o)); });


    public IOrder OnError(Action<IOrder, Exception> fnc) { 
      onError = (o, err) => OnErrorWrapper(o, err, fnc);
      return this;
    }
    public IOrder OnErrorAsync(Func<IOrder, Exception, Task> fnc) =>
      OnError((o, err) => { Task.Run(async () => await fnc(o, err)); });

    public IOrder Submit() {
      SubmitAsync().Wait();
      return this;
    }
    public async Task<IOrder> SubmitAsync() {
      try {
        Market.Subscribe(OnTradeListener);
        var res = Type switch {
          OrderType.Market => await Market.Exchange.CreateMarketOrder(Market, Side, Quantity),
          OrderType.Limit => await Market.Exchange.CreateLimitOrder(Market, Side, Quantity, Price),
          _ => throw new NotImplementedException("Unknown order type")
        };
        Id = res.OrderId;
        foreach (var trade in res.MatchedOrders)
          OnTradeListener(trade);
      } catch (Exception ex) {
        onError(this, ex);
      }
      return this;
    }

    public void Cancel() => CancelAsync().Wait();
    public async Task CancelAsync() {
      if(Id != -1 && State == OrderState.Open) {
        var isCanceled = await Market.Exchange.CancelOrder(Market, Id);
        if(isCanceled) {
          Market.Unsubscribe(OnTradeListener);
          State = OrderState.Canceled;
          onCancel(this);
        }
      }
    }

    public IOrder CancelAfter(TimeSpan timeSpan) {
      cancellationTokenSource.Token.Register(Cancel);
      cancellationTokenSource.CancelAfter(timeSpan);
      return this;
    }


    public IOrder CancelIf(Func<IOrder, ITrade, bool> predicate) {
      cancelIfPredicate = predicate;
      return this;
    }

    private void OnExecutedWrapper(IOrder order, Action<IOrder>? fnc = null) {
      Market.Unsubscribe(OnTradeListener);
      State = OrderState.Filled;
      fnc?.Invoke(order);
    }

    private void OnTradeWrapper(IOrder order, ITrade trade, Action<IOrder, ITrade>? fnc = null) {
      fnc?.Invoke(order, trade);
    }

    private void OnCancelWrapper(IOrder order, Action<IOrder>? fnc = null) {
      fnc?.Invoke(order);
    }

    private void OnErrorWrapper(IOrder order, Exception err, Action<IOrder, Exception>? fnc = null) {
      Market.Unsubscribe(OnTradeListener);
      State = OrderState.Faulted;
      Console.WriteLine($"Order [{this}] faulted!");
      fnc?.Invoke(order, err);
    }

    private void OnTradeListener(ITrade trade) {
      HandleEarlyTradeMatches(trade);

      if (trade.OIDSeller == Id || trade.OIDBuyer == Id) {
        trades.Add(trade);
        if (ExecutedQuantity >= Quantity)
          onExecuted(this);
        onTrade(this, trade);
      }
      if(cancelIfPredicate(this, trade))
        Cancel();
    }

    // buffer trades matching this order
    // -> when the response with the id takes longer as expected
    private void HandleEarlyTradeMatches(ITrade trade) {
      if (Id == -1)
        tmpTrades.Add(trade);
      else if (Id != -1 && tmpTrades.Count > 0) {
        var tmp = tmpTrades.ToArray();
        tmpTrades.Clear();
        foreach (var tmpTrade in tmp)
          OnTradeListener(tmpTrade);
      }
    }

    #region IDisposable Members
    private bool disposedValue = false;
    private void Dispose(bool disposing) {
      if (!disposedValue) {
        if(disposing)
          Cancel();
        disposedValue = true;
      }
    }

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
