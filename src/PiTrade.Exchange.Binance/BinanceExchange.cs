using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Base;
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
  public sealed class BinanceExchange : IExchange, IDisposable {
    private readonly object locker = new object();
    private readonly BinanceHttpClient client;
    private readonly IDictionary<IMarket, ISet<Action<ITrade>>> tradeSubscriptions;
    private CancellationTokenSource cancellationTokenSource;

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

            var baseAssetPrecision = symbol.QuoteAssetPrecision;
            var quoteAssetPrecision = symbol.BaseAssetPrecision;

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

    private static string MarketString(IMarket market) => $"{market.BaseAsset}{market.QuoteAsset}".ToUpper();

    public void Subscribe(IMarket market, Action<ITrade> onTrade) {
      if(!tradeSubscriptions.ContainsKey(market))
        tradeSubscriptions.Add(market, new HashSet<Action<ITrade>>());
      tradeSubscriptions[market].Add(onTrade);
    }

    public void Unsubscribe(IMarket market, Action<ITrade> onTrade) {
      if (tradeSubscriptions.ContainsKey(market))
        tradeSubscriptions[market].Remove(onTrade);
    }
    #endregion
    #region IDisposable Members
    private bool disposedValue;
    private void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing)
          cancellationTokenSource.Cancel();
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
