using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Logging;

namespace PiTrade.Exchange.Binance {
  internal class BinanceHttpClient {
#if DEBUG
    private const string BaseUri = "https://testnet.binance.vision";
#else
    private const string BaseUri = "https://api.binance.com";
#endif


    private readonly object locker = new object();
    private readonly string secret;
    private readonly HttpClient client;

    private long ping;
    internal long Ping {
      get { lock (locker) { return ping; } }
      set { lock (locker) { ping = value; } }
    }

    public BinanceHttpClient(string key, string secret) {
      this.secret = secret;
      client = new HttpClient();
      client.BaseAddress = new Uri(BaseUri);
      client.Timeout = TimeSpan.FromSeconds(10);
      client.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);
    }

    public Task<EmptyJsonResponse?> Send(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) =>
      Send<EmptyJsonResponse>(requestUri, method, query, content);

    public async Task<T?> Send<T>(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) where T : class {
      if (query == null)
        query = new Dictionary<string, object>();

      var queryString = PrepareQueryString(query, false);
      return await SendRequest<T>($"{requestUri}?{queryString}", method, content);
    }

    public Task<EmptyJsonResponse?> SendSigned(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) =>
      SendSigned<EmptyJsonResponse>(requestUri, method, query, content);

    public async Task<T?> SendSigned<T>(string requestUri, HttpMethod method, IDictionary<string, object>? query = null, object? content = null) where T : class {
      if (query == null)
        query = new Dictionary<string, object>();

      var queryString = PrepareQueryString(query);
      return await SendRequest<T>($"{requestUri}?{queryString}", method, content);
    }

    private async Task<T?> SendRequest<T>(string requestUri, HttpMethod method, object? content) where T : class {
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
            await Task.Delay(1000);
            return null;
          }
          return JsonConvert.DeserializeObject<T>(json);
        } catch (Exception e) {
          Log.Error(e.Message);
        }
        return null;
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
  }
}
