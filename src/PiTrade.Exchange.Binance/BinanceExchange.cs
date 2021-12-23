using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Logging;
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

    private long ping;
    private long Ping {
      get { lock (locker) { return ping; } }
      set { lock (locker) { ping = value; } }
    }

    public IEnumerable<IMarket> AvailableMarkets { get; private set; } = Enumerable.Empty<IMarket>();

    public BinanceExchange(string key, string secret) {
      this.secret = secret;
      client = new HttpClient();
      client.BaseAddress = new Uri(BaseUri);
      client.Timeout = TimeSpan.FromSeconds(10);
      client.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);
      InitExchange().Wait();
    }

    public async Task<IReadOnlyDictionary<Symbol, decimal>> GetFunds() {
      var response = await SendSigned<AccountInformation>("/api/v3/account", HttpMethod.Get);
      if(response == null) return new Dictionary<Symbol, decimal>();

      Dictionary<Symbol, decimal> funds = new Dictionary<Symbol, decimal>();
      foreach (var balance in response.Balances ?? Enumerable.Empty<AccountBalanceInformation>())
        if (balance.Asset != null)
          funds.Add(new Symbol(balance.Asset), balance.Free);

      return funds;
    }


    public IMarket? GetMarket(Symbol asset, Symbol quote) =>
      AvailableMarkets.Where(x => x.Asset == asset && x.Quote == quote).FirstOrDefault();

    #region Private Methods
    private async Task InitExchange() {
      var response = await Send<ExchangeInformation>("/api/v3/exchangeInfo", HttpMethod.Get);
      var markets = new List<IMarket>();

      if (response != null) { 
        Ping = response.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var symbol in response.Symbols ?? Enumerable.Empty<SymbolInformation>()) {
          if (symbol.BaseAsset != null && symbol.QuoteAsset != null && symbol.Filters != null) {
            var assetPrecision = symbol.Filters.Where(x => x.FilterType == "LOT_SIZE").Select(x => CalcPrecision(x.StepSize)).FirstOrDefault(-1);
            var quotePrecision = symbol.Filters.Where(x => x.FilterType == "PRICE_FILTER").Select(x => CalcPrecision(x.TickSize)).FirstOrDefault(-1);
            if (assetPrecision != -1 && quotePrecision != -1)
              markets.Add(new BinanceMarket(this,
                new Symbol(symbol.BaseAsset),
                new Symbol(symbol.QuoteAsset),
                assetPrecision, quotePrecision));
          }
        }
      }
      AvailableMarkets = markets;
      RefreshServerDeltaTime();
      //var timer = new Timer()
    }

    private void RefreshServerDeltaTime() =>
      Task.Delay(TimeSpan.FromMinutes(1))
          .ContinueWith(t => Send<ExchangeInformation>("/api/v3/time", HttpMethod.Get)
          .ContinueWith(r => {
            Ping = r.Result == null ? 0 : r.Result.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RefreshServerDeltaTime();
          }));
    #endregion

    #region Internal Methods
    internal async Task<Order> MarketOrder(IMarket market, OrderSide side, decimal quantity) {
      var response = await SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "MARKET"},
        {"quantity", quantity}
      });
      if (response == null || response.Id == -1)
        throw new Exception($"Response contains no order id ({response?.Id})");
      return new Order(response.Id, market, side, market.CurrentPrice, quantity);
    }

    internal async Task<Order> NewOrder(IMarket market, OrderSide side, decimal price, decimal quantity) {
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
        throw new Exception($"Response contains no order id ({response?.Id})");
      return new Order(response.Id, market, side, price, quantity);
    }

    internal async Task<Order> StopLoss(IMarket market, OrderSide side, decimal stopPrice, decimal quantity) {
      var response = await SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "STOP_LOSS"},
        {"quantity", quantity},
        {"stopPrice", stopPrice}
      });
      if (response == null || response.Id == -1)
        throw new Exception($"Response contains no order id ({response?.Id})");
      return new Order(response.Id, market, side, stopPrice, quantity);
    }

    internal Task Cancel(Order order) =>
      SendSigned("/api/v3/order", HttpMethod.Delete, new Dictionary<string, object>()
        { {"symbol", MarketString(order.Market) },
        {"orderId", order.Id.ToString()} });


    internal Task CancelAll(IMarket market) =>
      SendSigned("/api/v3/openOrders", HttpMethod.Delete, new Dictionary<string, object>()
        { {"symbol", MarketString(market)} });

    #endregion

    #region Helper
    private static int CalcPrecision(string? input) {
      if (input == null) return -1;
      var decimalSeparator = NumberFormatInfo.CurrentInfo.CurrencyDecimalSeparator;
      var digits = input.Split(decimalSeparator).Last();
      var position = digits.IndexOf("1");
      return (position == -1) ? 0 : position + 1;
    }

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
