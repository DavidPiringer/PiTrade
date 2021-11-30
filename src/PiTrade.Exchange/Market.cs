using System;
using System.Collections.Concurrent;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Exchange.Util;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public abstract class Market : IMarket {
    private readonly object locker = new object();
    private readonly IDictionary<long, PriceCandleTicker> priceCandleTickers = new Dictionary<long, PriceCandleTicker>();
    private readonly ConcurrentBag<IIndicator> indicators = new ConcurrentBag<IIndicator>();
    private readonly IList<MarketHandle> marketHandles = new List<MarketHandle>();

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public IEnumerable<IIndicator> Indicators => indicators.ToArray();

    public decimal CurrentPrice {
      get => currentPrice;
      protected set {
        lock (locker) {
          OnPriceUpdate(value);
          currentPrice = value;
        }
      }
    }

    private CancellationTokenSource CTS { get; set; } = new CancellationTokenSource();
    private Task? OrderFeedLoopTask { get; set; }

    private decimal currentPrice;

    public Market(IExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      Exchange = exchange;
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
    }

    public void AddIndicator(IIndicator indicator) {
      var key = (long)indicator.Period.TotalMilliseconds;
      PriceCandleTicker? ticker;
      if (!priceCandleTickers.TryGetValue(key, out ticker)) {
        ticker = new PriceCandleTicker(indicator.Period);
        priceCandleTickers.Add(key, ticker);
      }
      if (ticker != null) {
        ticker.Tick += indicator.Update;
        indicators.Add(indicator);
      }
    }

    public IMarketHandle GetMarketHandle(IOrderListener? listener = null) {
      if (OrderFeedLoopTask == null)
        OrderFeedLoopTask = TradeUpdateLoop(CTS.Token);
      
      var handle = new MarketHandle(this, listener);
      marketHandles.Add(handle);
      return handle;
    }

    public override string ToString() => $"{Asset}{Quote}";

    
    protected internal abstract Task<Order> NewOrder(OrderSide side, decimal price, decimal quantity);
    protected internal abstract Task CancelOrder(Order order);
    protected abstract Task InitTradeLoop();
    protected abstract Task<ITradeUpdate?> TradeUpdateLoopCycle(CancellationToken token);
    protected abstract Task ExitTradeLoop();

    internal void RemoveMarketHandle(MarketHandle handle) =>
      marketHandles.Remove(handle);

    protected void OnBuy(Order order) { 
    }

    private Task TradeUpdateLoop(CancellationToken token) =>
      Task.Factory.StartNew(async () => {
        while (token.IsCancellationRequested) {
          var update = await TradeUpdateLoopCycle(token);
          if(update != null)
            foreach (var handle in marketHandles) 
              handle.Update(update);
        }
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    

    private void OnPriceUpdate(decimal price) {
      foreach (var ticker in priceCandleTickers.Values)
        ticker.PriceUpdate(price);
    }

  }
}
