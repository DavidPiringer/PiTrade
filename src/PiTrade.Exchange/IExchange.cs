using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public interface IExchange {
    event Action<IMarket> MarketAdded;
    IEnumerable<IMarket> AvailableMarkets { get; }
    IMarket? GetMarket(Symbol asset, Symbol quote);
    Task<IReadOnlyDictionary<Symbol, decimal>> GetFunds();
  }
}
