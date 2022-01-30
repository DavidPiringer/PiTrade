using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public interface IExchange {
    Task<IMarket[]> GetMarkets();
    Task<IMarket?> GetMarket(Symbol asset, Symbol quote);

    Task Subscribe(params IMarket[] markets);
    Task Run(CancellationToken cancellationToken);
  }
}
