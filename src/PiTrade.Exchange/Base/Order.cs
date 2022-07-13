using PiTrade.Exchange.Enums;

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
    public decimal ExecutedAmount => ExecutedQuantity * AvgFillPrice;
    public decimal ExecutedQuantity { get; private set; }
    public decimal AvgFillPrice { get; private set; }
    public OrderState State { get; private set; }
    public IEnumerable<ITrade> Trades => trades.ToArray();


    private Action<IOrder, ITrade> onTrade;
    private Action<IOrder> onCancel;
    private Action<IOrder> onError;

    internal Order(IMarket market, OrderSide side, decimal quantity) {
      Id = -1;
      Market = market;
      Type = OrderType.Market;
      Side = side;
      Quantity = quantity;
      onTrade = (o, t) => OnTradeWrapper(o, t);
      onCancel = (o) => OnCancelWrapper(o);
      onError = (o) => OnErrorWrapper(o);
    }


    public override string ToString() =>
      //$"Id = {Id}, " +
      $"Market = {Market}, " +
      $"Side = {Side}, " +
      $"Price = {Price}({AvgFillPrice}), " +
      $"Quantity = {ExecutedQuantity}/{Quantity}";


    public IOrder For(decimal price) {
      Type = OrderType.Limit;
      Price = price;
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
      Id = Type switch {
        OrderType.Market => await Market.Exchange.CreateMarketOrder(Market, Side, Quantity),
        OrderType.Limit => await Market.Exchange.CreateLimitOrder(Market, Side, Quantity, Price),
        _ => throw new NotImplementedException("Unknown order type")
      };
      if (Id == -1)
        onError(this);
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

    private void OnTradeWrapper(IOrder order, ITrade trade, Action<IOrder, ITrade>? fnc = null) {
      if(trade.OIDSeller == Id || trade.OIDBuyer == Id) {
        trades.Add(trade);
        if (trades.Sum(x => x.Quantity) >= Quantity)
          State = OrderState.Filled;
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
