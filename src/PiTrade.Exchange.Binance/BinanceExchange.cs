using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PiTrade.Exchange.Binance {
  public sealed class BinanceExchange : IExchange {
    private readonly BinanceHttpClient client;


    public BinanceExchange(string key, string secret) {
      client = new BinanceHttpClient(key, secret);
    }


    public async Task<bool> CancelOrder(IMarket market, long orderId) {
      var res = await client.SendSigned("/api/v3/order", HttpMethod.Delete, new Dictionary<string, object>()
      { 
        {"symbol", MarketString(market) },
        {"orderId", orderId.ToString()} 
      });
      return res != null;
    }

    public async Task<long> CreateLimitOrder(IMarket market, OrderSide side, decimal quantity, decimal price) {
      var response = await client.SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "LIMIT"},
        {"timeInForce", "GTC"},
        {"quantity", quantity},
        {"price", price}
      });
      return response == null ? -1 : response.Id;
    }

    public async Task<long> CreateMarketOrder(IMarket market, OrderSide side, decimal quantity) {
      var response = await client.SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "MARKET"},
        {"quantity", quantity}
      });
      return response == null ? -1 : response.Id;
    }

    public Task<IMarket[]> GetMarkets() {
      throw new NotImplementedException();
    }

    private static string MarketString(IMarket market) => $"{market.QuoteAsset}{market.BaseAsset}".ToUpper();

    public void Subscribe(IMarket market, Action<ITrade> onTrade) {
      throw new NotImplementedException();
    }

    public void Unsubscribe(IMarket market, Action<ITrade> onTrade) {
      throw new NotImplementedException();
    }
  }
}
