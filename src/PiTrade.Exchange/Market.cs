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
      get { lock(locker) { return currentPrice; } }
      protected set { lock (locker) { currentPrice = value; } }
    }

    private CancellationTokenSource? CTS { get; set; }
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

    public IMarketHandle GetMarketHandle(out Task awaitTask, IOrderListener? listener = null) {
      if (OrderFeedLoopTask == null) {
        CTS = new CancellationTokenSource();
        OrderFeedLoopTask = TradeUpdateLoop(CTS.Token);
      }
        
      awaitTask = OrderFeedLoopTask;
      var handle = new MarketHandle(this, listener);
      marketHandles.Add(handle);
      return handle;
    }

    public override string ToString() => $"{Asset}{Quote}";


    protected internal abstract Task<Order> MarketOrder(OrderSide side, decimal quantity);
    protected internal abstract Task<Order> NewOrder(OrderSide side, decimal price, decimal quantity);
    protected internal abstract Task<Order> StopLossOrder(OrderSide side, decimal stopPrice, decimal quantity);
    protected internal abstract Task CancelOrder(Order order);
    protected abstract Task InitTradeLoop();
    protected abstract Task<ITradeUpdate?> TradeUpdateLoopCycle(CancellationToken token);
    protected abstract Task ExitTradeLoop();

    internal void RemoveMarketHandle(MarketHandle handle) {
      marketHandles.Remove(handle);
      // if no handles exists -> break order feed loop and cleanup
      if (marketHandles.Count == 0 && OrderFeedLoopTask != null && CTS != null) {
        CTS.Cancel();
        OrderFeedLoopTask.Wait();
        OrderFeedLoopTask.Dispose();
        OrderFeedLoopTask = null;
        CTS.Dispose();
        CTS = null;
      }
    }

    private Task TradeUpdateLoop(CancellationToken token) =>
      Task.Factory.StartNew(() => {
        InitTradeLoop().Wait();
        while (!token.IsCancellationRequested) {
          var update = TradeUpdateLoopCycle(token).GetAwaiter().GetResult();
          if (update != null) {
            CurrentPrice = update.Price;
            // update indicators
            foreach (var ticker in priceCandleTickers.Values)
              ticker.PriceUpdate(update.Price);
            // update marketHandles
            foreach (var handle in marketHandles)
              handle.Update(update);
          }
            
        }
        ExitTradeLoop().Wait();
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

  }
}
