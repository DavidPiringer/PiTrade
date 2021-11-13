using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace PiTrade.Exchange.Binance
{
  internal class BinanceHttpWrapper
  {
    private const string BaseUri = "https://api.binance.com";
    private readonly string Secret;
    private readonly HttpClient Client;

    public long ServerTimeDelta { get; set; } 


    public BinanceHttpWrapper(string key, string secret)
    {
      Secret = secret;
      Client = new HttpClient();
      Client.BaseAddress = new Uri(BaseUri);
      Client.Timeout = TimeSpan.FromSeconds(10);
      Client.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);
    }

    public async Task<string> Send(string requestUri, HttpMethod method, IDictionary<string, object> query, object? content = null)
    {
      var queryString = PrepareQueryString(query, false);
      return await SendRequest($"{requestUri}?{queryString}", method, content);
    }

    public async Task<string> SendSigned(string requestUri, HttpMethod method, IDictionary<string, object> query, object? content = null)
    {
      var queryString = PrepareQueryString(query);
      return await SendRequest($"{requestUri}?{queryString}", method, content);
    }

    private async Task<string> SendRequest(string requestUri, HttpMethod method, object? content)
    {
      using (var request = new HttpRequestMessage(method, $"{BaseUri}{requestUri}"))
      {
        if (content != null)
          request.Content = new StringContent(
            JsonConvert.SerializeObject(content),
            Encoding.UTF8, "application/json");

        var response = await Client.SendAsync(request);
        return await response.Content.ReadAsStringAsync();
      }
    }

    private string Sign(string rawData)
    {
      using (HMACSHA256 sha256Hash = new HMACSHA256(Encoding.UTF8.GetBytes(Secret)))
      {
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return BitConverter.ToString(bytes).Replace("-", "").ToLower();
      }
    }

    private string PrepareQueryString(IDictionary<string, object> query, bool sign = true)
    {
      if (sign)
      {
        query.Add("recvWindow", 5000);
        query.Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ServerTimeDelta);
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
