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
    private readonly bool finishMarketSubscriptionOnRun;
    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(0);
    private readonly IList<IMarket> markets = new List<IMarket>();

    private DateTime lastFetchMarketsUpdate = DateTime.MinValue;

    public Exchange(IExchangeStreamClient api) : this(api, TimeSpan.FromMinutes(5)) { }

    public Exchange(IExchangeStreamClient api, TimeSpan fetchMarketsUpdateInterval, bool finishMarketSubscriptionOnRun = true) {
      this.api = api;
      this.fetchMarketsUpdateInterval = fetchMarketsUpdateInterval;
      this.finishMarketSubscriptionOnRun = finishMarketSubscriptionOnRun;
    }

    public async Task<IMarket[]> GetMarkets() {
      if (!markets.Any())
        await UpdateMarkets();
      return markets.ToArray();
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

    private async Task StartMarketStreams(IMarket[] marketArr) {
      var step = (int)api.MaxMarketCountPerStream;
      for (int i = 0; i < marketArr.Length; i += step) {
        var length = Math.Min(marketArr.Length - i, step);
        var webSocket = await api.GetStream(marketArr.AsSpan(i, length).ToArray());
        EnqueueLoop(webSocket);
      }
    }

    public async Task Run(CancellationToken cancellationToken) {
      var marketArr = subscribedMarkets.ToArray();
      // start websocket for subscribedMarkets
      if (finishMarketSubscriptionOnRun) // add this logic into binanceClient?
        await StartMarketStreams(marketArr);

      while (!cancellationToken.IsCancellationRequested) {
        // wait for semaphore to prevent the loop running without updates
        await semaphore.WaitAsync(cancellationToken);

        // update markets
        if (tradeUpdates.TryDequeue(out ITradeUpdate? update) && update != null) {
          var markets = finishMarketSubscriptionOnRun ? marketArr : subscribedMarkets.ToArray();
          var market = SearchMarket(markets, update.Asset, update.Quote);
          if (market != null && market is Market m) await m.Update(update);
        }

        // fetch markets
        if (lastFetchMarketsUpdate.Add(fetchMarketsUpdateInterval) < DateTime.Now) {
          lastFetchMarketsUpdate = DateTime.Now;
          await UpdateMarkets();
        }
      }
    }

    private async Task UpdateMarkets() {
      var dtos = await api.FetchMarkets();
      foreach (var dto in dtos) {
        bool marketExists = false;
        foreach(var market in markets) {
          if(market.Asset == dto.Asset && market.Quote == dto.Quote) {
            marketExists = true;
            // update market
          }
        }
        if(!marketExists) markets.Add(new Market(this, api, dto));
      }
    }
  }
}
