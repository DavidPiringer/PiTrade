using System.Linq;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;

namespace PiTrade.Exchange.Base {
  public sealed class Order : IOrder {
    private readonly IList<ITrade> trades = new List<ITrade>();

    public long Id { get; private set; }
    public IMarket Market { get; private set; }
    public OrderType Type { get; private set; }
    public OrderSide Side { get; private set; }
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Amount => Quantity * Price;
    public decimal ExecutedPrice => Trades.Average(x => x.Price);
    public decimal ExecutedQuantity => Trades.Sum(x => x.Quantity);
    public decimal ExecutedAmount => ExecutedQuantity * ExecutedPrice;
    public OrderState State { get; private set; }
    public IEnumerable<ITrade> Trades => trades.ToArray();


    private Action<IOrder> onExecuted;
    private Action<IOrder, ITrade> onTrade;
    private Action<IOrder> onCancel;
    private Action<IOrder> onError;

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
      onError = (o) => OnErrorWrapper(o);
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

    public IOrder OnTrade(Action<IOrder, ITrade> fnc) {
      onTrade = (o, t) => OnTradeWrapper(o, t, fnc);
      return this;
    }

    public IOrder OnCancel(Action<IOrder> fnc) {
      onCancel = (o) => OnCancelWrapper(o, fnc);
      return this;
    }
    public IOrder OnError(Action<IOrder> fnc) { 
      onError = (o) => OnErrorWrapper(o, fnc);
      return this;
    }

    public async Task<IOrder> Transmit() {
      Market.Subscribe(OnTradeListener);
      var res = Type switch {
        OrderType.Market => await Market.Exchange.CreateMarketOrder(Market, Side, Quantity),
        OrderType.Limit => await Market.Exchange.CreateLimitOrder(Market, Side, Quantity, Price),
        _ => throw new NotImplementedException("Unknown order type")
      };
      Id = res.OrderId;
      if (Id == -1)
        onError(this);
      foreach (var trade in res.MatchedOrders)
        onTrade(this, trade);
      return this;
    }

    public async Task Cancel() {
      if(Id != -1 && State == OrderState.Open) {
        Market.Unsubscribe(OnTradeListener);
        State = OrderState.Canceled;
        await Market.Exchange.CancelOrder(Market, Id);
        onCancel(this);
      }
    }

    private void OnExecutedWrapper(IOrder order, Action<IOrder>? fnc = null) {
      Market.Unsubscribe(OnTradeListener);
      State = OrderState.Filled;
      fnc?.Invoke(order);
    }
    private void OnTradeWrapper(IOrder order, ITrade trade, Action<IOrder, ITrade>? fnc = null) {
      if(trade.OIDSeller == Id || trade.OIDBuyer == Id) {
        trades.Add(trade);
        if (trades.Sum(x => x.Quantity) >= Quantity)
          onExecuted(order);
        fnc?.Invoke(order, trade);
      }
    }

    private void OnCancelWrapper(IOrder order, Action<IOrder>? fnc = null) {
      State = OrderState.Canceled;
      fnc?.Invoke(order);
    }

    private void OnErrorWrapper(IOrder order, Action<IOrder>? fnc = null) {
      State = OrderState.Faulted;
      fnc?.Invoke(order);
    }

    private void OnTradeListener(ITrade trade) => onTrade(this, trade);

    #region IDisposable Members
    private bool disposedValue = false;
    private void Dispose(bool disposing) {
      if (!disposedValue) {
        if(disposing)
          Cancel().Wait();
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
