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
    private readonly IList<IIndicator> indicators = new List<IIndicator>();
    private readonly IList<Order> openOrders = new List<Order>();
    private readonly IList<Func<decimal, Task>> listeners = new List<Func<decimal, Task>>();

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public IEnumerable<IIndicator> Indicators => indicators.ToArray();

    public decimal CurrentPrice { get; private set; }

    private CancellationTokenSource? CTS { get; set; }
    private Task? MarketLoop { get; set; }



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
        ticker.AddIndicator(indicator);
        indicators.Add(indicator);
      }
    }

    public void Listen(Func<decimal, Task> fnc) => listeners.Add(fnc);


    public Task<(Order? order, ErrorState error)> CreateMarketOrder(OrderSide side, decimal quantity) =>
      NewMarketOrder(side, quantity.RoundDown(AssetPrecision));
    public abstract Task<(Order? order, ErrorState error)> NewMarketOrder(OrderSide side, decimal quantity);

    public Task<(Order? order, ErrorState error)> CreateLimitOrder(OrderSide side, decimal price, decimal quantity) =>
      NewLimitOrder(side, price.RoundDown(QuotePrecision), quantity.RoundUp(AssetPrecision));
    public abstract Task<(Order? order, ErrorState error)> NewLimitOrder(OrderSide side, decimal price, decimal quantity);

    public Task Connect() {
      if (MarketLoop == null) {
        CTS = new CancellationTokenSource();
        MarketLoop = CreateMarketLoop(CTS.Token);
      }
      return MarketLoop;
    }


    /// <summary>
    /// Registers a order to receive updates. This is called in order's constructor.
    /// With this 'trick' it is possible to use the interface signature for child market 
    /// types and still have a reference to all created orders.
    /// </summary>
    internal void RegisterOrder(Order order) {
      lock (locker) openOrders.Add(order); 
    }

    protected internal abstract Task<ErrorState> CancelOrder(Order order);
    protected virtual Task InitMarketLoop() => Task.CompletedTask;
    protected abstract Task<ITradeUpdate?> MarketLoopCycle(CancellationToken token);
    protected virtual Task ExitMarketLoop() => Task.CompletedTask;

    private Task CreateMarketLoop(CancellationToken token) { 
      var task = Task.Factory.StartNew(async () => {
        await InitMarketLoop();
        while (!token.IsCancellationRequested) {
          var update = await MarketLoopCycle(token);
          if (update != null) {
            // update only if price has changed
            if (CurrentPrice != update.Price) {
              lock (locker) CurrentPrice = update.Price;
              // update indicators
              foreach (var ticker in priceCandleTickers.Values)
                ticker.PriceUpdate(update.Price);
              // notify listeners
              foreach (var listener in listeners)
                await listener(update.Price);
            }

            // update open orders
            foreach (var order in openOrders.ToArray()) {
              order.Update(update);
              // remove filled or cancelled orders
              if (order.IsFilled || order.IsCancelled) 
                lock(locker) openOrders.Remove(order);
            }
          }
        }
        await ExitMarketLoop();
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
      task.Wait();
      return task.Result;
    }

  }
}
