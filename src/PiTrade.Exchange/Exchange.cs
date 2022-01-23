using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public abstract class Exchange : IExchange {
    private readonly IList<Market> markets = new List<Market>();

    public IEnumerable<IMarket> AvailableMarkets => markets;

    public event Action<IMarket>? MarketAdded;

    public IMarket? GetMarket(Symbol asset, Symbol quote) =>
      markets.Where(x => x.Asset == asset && x.Quote == quote).FirstOrDefault();

    public void Run(CancellationToken cancellationToken) {
      IList<Task> tasks = new List<Task>();
      while (!cancellationToken.IsCancellationRequested) {
        foreach (var market in markets.ToArray())
          if (market.IsEnabled) tasks.Add(market.Update());
        tasks.Add(Update(cancellationToken));
        Task.WaitAll(tasks.ToArray());
        tasks.Clear();
      }
    }

    protected abstract Task Update(CancellationToken cancellationToken);

    protected void RegisterMarket(Market market) {
      markets.Add(market);
      MarketAdded?.Invoke(market);
    }
  }
}
