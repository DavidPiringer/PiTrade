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
    private IMarket[]? markets;

    public Exchange(IExchangeStreamClient api) { 
      this.api = api;
    }

    public async Task<IMarket[]> GetMarkets() {
      if(markets == null) // TODO: update interval?? cache is only valid for x period time
        markets = (await api.FetchMarkets()).Select(x => new Market(this, api, x)).ToArray();
      return markets;
    }

    public async Task<IMarket?> GetMarket(Symbol asset, Symbol quote) =>
      SearchMarket(await GetMarkets(), asset, quote);

    public async Task Subscribe(params IMarket[] markets) {
      subscribedMarkets.AddRange(markets);
      var webSocket = await api.GetStream(markets);
      EnqueueLoop(webSocket);
    }

    private void EnqueueLoop(WebSocket<ITradeUpdate> webSocket) => 
      webSocket.NextMessage().ContinueWith(t => {
        t.Wait();
        (ITradeUpdate? update, bool success) = t.Result;
        if (success && update != null)
          tradeUpdates.Enqueue(update);
        EnqueueLoop(webSocket);
      });
      
    private IMarket? SearchMarket(IEnumerable<IMarket> m, Symbol asset, Symbol quote) =>
      m.Where(x => x.Asset == asset && x.Quote == quote).FirstOrDefault();

    public void Run(CancellationToken cancellationToken) {
      IList<Task> tasks = new List<Task>();
      while (!cancellationToken.IsCancellationRequested) {
        // iterate markets
        while (tradeUpdates.Count > 0 && !cancellationToken.IsCancellationRequested) { 
          if(tradeUpdates.TryDequeue(out ITradeUpdate? update) && update != null) {
            var market = SearchMarket(subscribedMarkets.ToArray(), update.Asset, update.Quote);
            if(market != null && market is Market m) tasks.Add(m.Update(update));
          }
        }
        Task.WaitAll(tasks.ToArray());
        tasks.Clear();
      }
    }
  }
}
