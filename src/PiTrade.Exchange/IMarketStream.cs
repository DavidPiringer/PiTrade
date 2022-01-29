using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange {
  public interface IMarketStream {
    Task Subscribe(IMarket market);
    Task Unsubscribe(IMarket market);
    Task<ITradeUpdate> NextUpdate();
  }
}
