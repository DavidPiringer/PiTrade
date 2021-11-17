using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Extensions;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace PiTrade.Exchange.Binance {
  public sealed class BinanceExchange : IExchange {
    private const string BaseUri = "https://api.binance.com";
    private readonly string Secret;
    private readonly HttpClient Client;
    private readonly object locker = new object();

    private long serverDeltaTime;
    private long ServerDeltaTime {
      get { lock (locker) { return serverDeltaTime; } }
      set { lock (locker) { serverDeltaTime = value; } }
    }

    public IEnumerable<IMarket> AvailableMarkets { get; private set; } = Enumerable.Empty<IMarket>();

    public BinanceExchange(string key, string secret) {
      Secret = secret;
      Client = new HttpClient();
      Client.BaseAddress = new Uri(BaseUri);
      Client.Timeout = TimeSpan.FromSeconds(10);
      Client.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);
      InitExchange().Wait();
    }

    public async Task<IReadOnlyDictionary<Symbol, decimal>> GetFunds() {
      var response = await SendSigned<AccountInformation>("/api/v3/account", HttpMethod.Get);
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
      ServerDeltaTime = response.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

      var markets = new List<IMarket>();
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
      AvailableMarkets = markets;
      RefreshServerDeltaTime();
    }

    private void RefreshServerDeltaTime() =>
      Task.Delay(TimeSpan.FromMinutes(1))
          .ContinueWith(t => Send<ExchangeInformation>("/api/v3/time", HttpMethod.Get)
          .ContinueWith(r => {
            ServerDeltaTime = r.Result.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RefreshServerDeltaTime();
          }));
    #endregion

    #region Internal Methods
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

    private Task<EmptyJsonResponse> Send(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) =>
      Send<EmptyJsonResponse>(requestUri, method, query, content);

    private async Task<T> Send<T>(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) {
      if (query == null)
        query = new Dictionary<string, object>();

      var queryString = PrepareQueryString(query, false);
      return await SendRequest<T>($"{requestUri}?{queryString}", method, content);
    }

    private Task<EmptyJsonResponse> SendSigned(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) =>
      SendSigned<EmptyJsonResponse>(requestUri, method, query, content);

    private async Task<T> SendSigned<T>(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) {
      if (query == null)
        query = new Dictionary<string, object>();

      var queryString = PrepareQueryString(query);
      return await SendRequest<T>($"{requestUri}?{queryString}", method, content);
    }

    private async Task<T> SendRequest<T>(string requestUri, HttpMethod method, object? content) {
      using (var request = new HttpRequestMessage(method, $"{BaseUri}{requestUri}")) {
        if (content != null)
          request.Content = new StringContent(
            JsonConvert.SerializeObject(content),
            Encoding.UTF8, "application/json");

        var response = await Client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) {
          Console.WriteLine(response);
          Console.WriteLine(json);
        }

#pragma warning disable CS8603 // Possible null reference return.
        return typeof(T) != typeof(EmptyJsonResponse) ?
               JsonConvert.DeserializeObject<T>(json) : default(T);
#pragma warning restore CS8603 // Possible null reference return.
      }
    }

    private string Sign(string rawData) {
      using (HMACSHA256 sha256Hash = new HMACSHA256(Encoding.UTF8.GetBytes(Secret))) {
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
      }
    }

    private string PrepareQueryString(IDictionary<string, object> query, bool sign = true) {
      if (sign) {
        query.Add("recvWindow", 20000);
        query.Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ServerDeltaTime);
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
