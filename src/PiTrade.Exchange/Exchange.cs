using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Logging;

namespace PiTrade.Exchange {
  public abstract class Exchange : IExchange {
    private readonly IList<Market> markets = new List<Market>();

    public IEnumerable<IMarket> AvailableMarkets => markets;

    public event Action<IMarket>? MarketAdded;

    public IMarket? GetMarket(Symbol asset, Symbol quote) {
      var market = markets.Where(x => x.Asset == asset && x.Quote == quote).FirstOrDefault();
      market?.Connect().Wait();
      return market;
    }

    public async Task Run(CancellationToken cancellationToken) {
      /*
      IList<Task> tasks = new List<Task>();
      foreach (var market in markets)
        tasks.Add(market.Connect());
      Task.WaitAll(tasks.ToArray());
      */
      while (!cancellationToken.IsCancellationRequested) {
        var enabledMarkets = markets.Where(x => x.IsEnabled).ToArray();

        // return is no market is enabled
        if (!enabledMarkets.Any()) return;

        // iterate markets
        foreach (var market in enabledMarkets) {
          Log.Info($"market = {market.Asset}{market.Quote}");
          await market.Update();
        }
        await Update(cancellationToken);
        //Task.WaitAll(tasks.ToArray());
        //tasks.Clear();
      }

      foreach (var market in markets)
        await market.Disconnect();
    }

    protected abstract Task Update(CancellationToken cancellationToken);

    protected void RegisterMarket(Market market) {
      markets.Add(market);
      MarketAdded?.Invoke(market);
    }
  }
}
