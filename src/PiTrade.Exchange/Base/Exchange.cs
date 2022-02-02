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
  public sealed class Exchange : IExchange {
    private readonly IExchangeStreamClient api;
    private readonly ConcurrentQueue<ITradeUpdate> tradeUpdates = new ConcurrentQueue<ITradeUpdate>();
    private readonly List<IMarket> subscribedMarkets = new List<IMarket>();
    private readonly TimeSpan fetchMarketsUpdateInterval;
    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
    private readonly bool finishMarketSubscriptionOnRun;
    private IMarket[]? markets;

    private DateTime lastFetchmarketsUpdate = DateTime.MinValue;

    public Exchange(IExchangeStreamClient api) : this(api, TimeSpan.FromMinutes(5)) { }

    public Exchange(IExchangeStreamClient api, TimeSpan fetchMarketsUpdateInterval, bool finishMarketSubscriptionOnRun = true) {
      this.api = api;
      this.fetchMarketsUpdateInterval = fetchMarketsUpdateInterval;
      this.finishMarketSubscriptionOnRun = finishMarketSubscriptionOnRun;
    }

    public async Task<IMarket[]> GetMarkets() {
      if (markets == null)
        markets = await FetchMarkets();
      return markets;
    }

    public async Task<IMarket?> GetMarket(Symbol asset, Symbol quote) =>
      SearchMarket(await GetMarkets(), asset, quote);

    public async Task Subscribe(params IMarket[] markets) {
      subscribedMarkets.AddRange(markets);
      if (!finishMarketSubscriptionOnRun)
        await StartMarketStreams(markets);
    }

    private void EnqueueLoop(WebSocket<ITradeUpdate> webSocket) => 
      webSocket.NextMessage().ContinueWith(t => {
        t.Wait();
        (ITradeUpdate? update, bool success) = t.Result;
        if (success && update != null) {
          tradeUpdates.Enqueue(update);
          semaphore.Release();
        }
        EnqueueLoop(webSocket);
      });
      
    private IMarket? SearchMarket(IEnumerable<IMarket> m, Symbol asset, Symbol quote) =>
      m.Where(x => x.Asset == asset && x.Quote == quote).FirstOrDefault();

    private async Task<IMarket[]> FetchMarkets() =>
      (await api.FetchMarkets()).Select(x => new Market(this, api, x)).ToArray();

    private async Task StartMarketStreams(IEnumerable<IMarket> markets) {
      var marketArr = markets.ToArray();
      var step = (int)api.MaxMarketCountPerStream;
      for (int i = 0; i < marketArr.Length; i += step) {
        var length = Math.Min(marketArr.Length - i, step);
        var webSocket = await api.GetStream(marketArr.AsSpan(i, length).ToArray());
        EnqueueLoop(webSocket);
      }
    }

    public async Task Run(CancellationToken cancellationToken) {
      // start websocket for subscribedMarkets
      if(finishMarketSubscriptionOnRun)
        await StartMarketStreams(subscribedMarkets);

      //IList<Task> tasks = new List<Task>();
      while (!cancellationToken.IsCancellationRequested) {
        await semaphore.WaitAsync(cancellationToken);
               
        // update markets
        if (tradeUpdates.TryDequeue(out ITradeUpdate? update) && update != null) {
          var market = SearchMarket(subscribedMarkets.ToArray(), update.Asset, update.Quote);
          if(market != null && market is Market m) await m.Update(update);
        }

        // fetch markets
        if (lastFetchmarketsUpdate.Add(fetchMarketsUpdateInterval) < DateTime.Now) {
          lastFetchmarketsUpdate = DateTime.Now;
          markets = await FetchMarkets();
        }
        //Task.WaitAll(tasks.ToArray());
        //tasks.Clear();
      }
    }
  }
}
