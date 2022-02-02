using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public interface IExchangeStreamClient : IExchangeAPIClient {
    uint MaxMarketCountPerStream { get; }
    Task<WebSocket<ITradeUpdate>> GetStream(params IMarket[] markets);
  }
}
