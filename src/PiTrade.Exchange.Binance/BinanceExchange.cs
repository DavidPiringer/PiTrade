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
    private const string BaseUri = "https://api.binance.com";
    private readonly string secret;
    private readonly HttpClient client;
    private readonly object locker = new object();
    private readonly ConcurrentDictionary<string, IMarket> marketDict
      = new ConcurrentDictionary<string, IMarket>();

    private long ping;
    private long Ping {
      get { lock (locker) { return ping; } }
      set { lock (locker) { ping = value; } }
    }


    public event Action<IMarket> MarketAdded;

    public IEnumerable<IMarket> AvailableMarkets => marketDict.Values;

    public BinanceExchange(string key, string secret) {
      this.secret = secret;
      client = new HttpClient();
      client.BaseAddress = new Uri(BaseUri);
      client.Timeout = TimeSpan.FromSeconds(10);
      client.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);
      UpdateExchange().Wait();
    }

    public async Task<IReadOnlyDictionary<Symbol, decimal>> GetFunds() {
      var response = await SendSigned<AccountInformation>("/api/v3/account", HttpMethod.Get);
      if (response == null) return new Dictionary<Symbol, decimal>();

      Dictionary<Symbol, decimal> funds = new Dictionary<Symbol, decimal>();
      foreach (var balance in response.Balances ?? Enumerable.Empty<AccountBalanceInformation>())
        if (balance.Asset != null)
          funds.Add(new Symbol(balance.Asset), balance.Free);

      return funds;
    }


    public IMarket? GetMarket(Symbol asset, Symbol quote) =>
      AvailableMarkets.Where(x => x.Asset == asset && x.Quote == quote).FirstOrDefault();

    #region Private Methods
    private async Task UpdateExchange() {
      var response = await Send<ExchangeInformation>("/api/v3/exchangeInfo", HttpMethod.Get);

      if (response != null) {
        Ping = response.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var symbols = response.Symbols ?? Enumerable.Empty<SymbolInformation>();
        foreach (var symbol in symbols) {
          if (symbol.BaseAsset != null &&
              symbol.QuoteAsset != null &&
              symbol.Filters != null &&
              symbol.IsSpotTradingAllowed.HasValue &&
              symbol.IsSpotTradingAllowed.Value &&
              symbol.MarketString != null &&
              !marketDict.ContainsKey(symbol.MarketString)) {

            var assetPrecision = symbol.AssetPrecision;
            var quotePrecision = symbol.QuotePrecision;

            if (assetPrecision.HasValue && quotePrecision.HasValue) {
              var market = new BinanceMarket(this, new Symbol(symbol.BaseAsset), new Symbol(symbol.QuoteAsset),
                                             assetPrecision.Value, quotePrecision.Value);
              if (marketDict.TryAdd(symbol.MarketString, market)) {
                MarketAdded?.Invoke(market);
              }
            }
          }
        }
      }
      RefreshServerDeltaTime();
    }

    private void RefreshServerDeltaTime() =>
      Task.Delay(TimeSpan.FromMinutes(5))
          .ContinueWith(r => UpdateExchange().Wait());
    #endregion

    #region Internal Methods
    internal async Task<(Order? order, ErrorState error)> NewMarketOrder(Market market, OrderSide side, decimal quantity) {
      var response = await SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "MARKET"},
        {"quantity", quantity}
      });
      if (response == null || response.Id == -1)
        return (null, ErrorState.IdNotFound);
      return (new Order(response.Id, market, side, market.CurrentPrice, quantity), ErrorState.None);
    }

    internal async Task<(Order? order, ErrorState error)> NewLimitOrder(Market market, OrderSide side, decimal price, decimal quantity) {
      var response = await SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "LIMIT"},
        {"timeInForce", "GTC"},
        {"quantity", quantity},
        {"price", price}
      });
      if (response == null || response.Id == -1)
        return (null, ErrorState.IdNotFound);
      return (new Order(response.Id, market, side, market.CurrentPrice, quantity), ErrorState.None);
    }


    internal async Task<ErrorState> Cancel(Order order) =>
      await SendSigned("/api/v3/order", HttpMethod.Delete, new Dictionary<string, object>()
        { {"symbol", MarketString(order.Market) },
        {"orderId", order.Id.ToString()} }) == null ?
      ErrorState.ConnectionLost : ErrorState.None;


    internal Task CancelAll(IMarket market) =>
      SendSigned("/api/v3/openOrders", HttpMethod.Delete, new Dictionary<string, object>()
        { {"symbol", MarketString(market)} });

    #endregion

    public override string ToString() => "Binance";

    #region Helper
    private static string MarketString(IMarket market) => $"{market.Asset}{market.Quote}".ToUpper();
    #endregion

    #region Http Client Abstraction

    private Task<EmptyJsonResponse?> Send(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) =>
      Send<EmptyJsonResponse>(requestUri, method, query, content);

    private async Task<T?> Send<T>(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) {
      if (query == null)
        query = new Dictionary<string, object>();

      var queryString = PrepareQueryString(query, false);
      return await SendRequest<T>($"{requestUri}?{queryString}", method, content);
    }

    private Task<EmptyJsonResponse?> SendSigned(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) =>
      SendSigned<EmptyJsonResponse>(requestUri, method, query, content);

    private async Task<T?> SendSigned<T>(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) {
      if (query == null)
        query = new Dictionary<string, object>();

      var queryString = PrepareQueryString(query);
      return await SendRequest<T>($"{requestUri}?{queryString}", method, content);
    }

    private async Task<T?> SendRequest<T>(string requestUri, HttpMethod method, object? content) {
      using (var request = new HttpRequestMessage(method, $"{BaseUri}{requestUri}")) {
        if (content != null)
          request.Content = new StringContent(
            JsonConvert.SerializeObject(content),
            Encoding.UTF8, "application/json");

        try {
          var response = await client.SendAsync(request);
          var json = await response.Content.ReadAsStringAsync();
          if (!response.IsSuccessStatusCode) {
            Log.Error(requestUri);
            Log.Error(response);
            Log.Error(json);
            // TODO: deserialize in binance error response
          }
          return typeof(T) != typeof(EmptyJsonResponse) ?
                 JsonConvert.DeserializeObject<T>(json) : default(T);
        } catch (Exception e) {
          Log.Error(e.Message);
        }
        return default(T);
      }
    }

    private string Sign(string rawData) {
      using (HMACSHA256 sha256Hash = new HMACSHA256(Encoding.UTF8.GetBytes(secret))) {
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
      }
    }

    private string PrepareQueryString(IDictionary<string, object> query, bool sign = true) {
      if (sign) {
        query.Add("recvWindow", 20000);
        query.Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + Ping);
      }

      var queryString = string.Join("&", query
        .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value?.ToString()))
        .Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value.ToString())}"));

      if (sign)
        queryString = $"{queryString}&signature={Sign(queryString)}";

      return queryString;
    }
    #endregion
  }
}
