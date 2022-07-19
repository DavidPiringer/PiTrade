using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Base;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Logging;
using PiTrade.Networking;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PiTrade.Exchange.Binance {
  public sealed class BinanceExchange : IExchange, IDisposable {
//#if DEBUG
//    private const string WSBaseUri = "wss://testnet.binance.vision/ws";
//#else
    private const string WSBaseUri = "wss://stream.binance.com:9443/ws";
//#endif
    private readonly object locker = new object();
    private readonly BinanceHttpClient client;
    private readonly IDictionary<IMarket, ISet<Action<ITrade>>> tradeSubscriptions;
    private CancellationTokenSource cancellationTokenSource;
    private IList<WebSocket> sockets = new List<WebSocket>();

    public decimal MinimalAmountPerOrder => 10.0m;

    private IEnumerable<IMarket> markets = Enumerable.Empty<IMarket>();
    public IEnumerable<IMarket> Markets {
      get { lock (locker) { return markets; } }
      private set { lock (locker) { markets = value; } }
    }



    private BinanceExchange(string key, string secret) {
      cancellationTokenSource = new CancellationTokenSource();
      client = new BinanceHttpClient(key, secret);
      tradeSubscriptions = new ConcurrentDictionary<IMarket, ISet<Action<ITrade>>>();
    }

    public static async Task<BinanceExchange> Create(string key, string secret) {
      var exchange = new BinanceExchange(key, secret);
      await exchange.FetchMarkets();
      exchange.SelfUpdateLoop();
      return exchange;
    }

    private void SelfUpdateLoop() => Task.Run(async () => {
      while (!cancellationTokenSource.Token.IsCancellationRequested) {
        await FetchMarkets();
        await Task.Delay(TimeSpan.FromMinutes(1), cancellationTokenSource.Token);
      }
    }, cancellationTokenSource.Token);

    private async Task FetchMarkets() {
      var response = await client.Send<ExchangeInformation>("/api/v3/exchangeInfo", HttpMethod.Get);
      var markets = new List<IMarket>();
      if (response != null) {
        client.Ping = response.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var symbols = response.Symbols ?? Enumerable.Empty<SymbolInformation>();
        foreach (var symbol in symbols) {
          if (symbol.BaseAsset != null &&
              symbol.QuoteAsset != null &&
              symbol.Filters != null &&
              symbol.MarketString != null) {

            var baseAssetPrecision = symbol.BaseAssetPrecision; 
            var quoteAssetPrecision = symbol.QuoteAssetPrecision;

            if (quoteAssetPrecision.HasValue && baseAssetPrecision.HasValue) {
              var baseAsset = new Symbol(symbol.BaseAsset);
              var quoteAsset = new Symbol(symbol.QuoteAsset);
              markets.Add(new Market(this, baseAsset, quoteAsset, baseAssetPrecision.Value, quoteAssetPrecision.Value));
            }
          }
        }
      }
      Markets = markets;
    }

#region IExchange Members
    public async Task<bool> CancelOrder(IMarket market, long orderId) {
      var res = await client.SendSigned("/api/v3/order", HttpMethod.Delete, new Dictionary<string, object>()
      { 
        {"symbol", MarketString(market) },
        {"orderId", orderId.ToString()} 
      });
      return res != null;
    }

    public async Task<OrderCreationResult> CreateLimitOrder(IMarket market, OrderSide side, decimal quantity, decimal price) {
      var response = await client.SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "LIMIT"},
        {"timeInForce", "GTC"},
        {"quantity", quantity},
        {"price", price}
      });
      return CreateOrderCreationResult(response);
    }

    public async Task<OrderCreationResult> CreateMarketOrder(IMarket market, OrderSide side, decimal quantity) {
      var response = await client.SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "MARKET"},
        {"quantity", quantity}
      });
      return CreateOrderCreationResult(response);
    }

    private static string MarketString(IMarket market) => $"{market.BaseAsset}{market.QuoteAsset}".ToUpper();

    public void Subscribe(IMarket market, Action<ITrade> onTrade) {
      if (!tradeSubscriptions.ContainsKey(market)) { 
        var hs = new HashSet<Action<ITrade>>();
        tradeSubscriptions.Add(market, hs);
        StartMarketWS(market, hs);
      }
      tradeSubscriptions[market].Add(onTrade);
    }

    public void Unsubscribe(IMarket market, Action<ITrade> onTrade) {
      if (tradeSubscriptions.ContainsKey(market))
        tradeSubscriptions[market].Remove(onTrade);
    }

#endregion

    private void StartMarketWS(IMarket market, IEnumerable<Action<ITrade>> subs) {
      WebSocket socket = new WebSocket(new Uri($"{WSBaseUri}/{MarketString(market).ToLower()}@trade"));
      socket.OnMessage(msg => ProcessTradeMessage(msg, subs));
      socket.Connect().Wait();
      sockets.Add(socket);
    }

    private void ProcessTradeMessage(string msg, IEnumerable<Action<ITrade>> subs) {
      var trade = JsonConvert.DeserializeObject<BinanceSpotTrade>(msg);
      if (trade == null) return;
      DelegateTrade(trade, subs);
    }

    private void DelegateTrade(ITrade trade, IEnumerable<Action<ITrade>> subs) {
      foreach (var sub in subs)
        sub(trade);
    }

    private OrderCreationResult CreateOrderCreationResult(BinanceOrder? order) {
      var oid = order?.Id ?? -1;
      var matches = (order?.Fills?.Select(x => x.ToSpotTrade()) ?? Enumerable.Empty<BinanceSpotTrade>()).ToArray();
      foreach(var match in matches)
        match.OIDBuyer = oid;

      return new OrderCreationResult() {
        OrderId = oid,
        MatchedOrders = matches
      };
    }
      

    #region IDisposable Members
    private bool disposedValue;
    private void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
          cancellationTokenSource.Cancel();
          foreach(var ws in sockets)
            ws.Disconnect().Wait();
        }
        disposedValue = true;
      }
    }

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
#endregion
  }
}
