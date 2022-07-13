using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.DTOs;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;
using PiTrade.Networking;

namespace PiTrade.Exchange.Binance {
  public class BinanceStreamAPIClient : IExchangeStreamClient {
    private const string BaseUri = "https://api.binance.com";
    private const string WSBaseUri = "wss://stream.binance.com:9443/ws";
    private readonly string secret;
    private readonly HttpClient client;
    private readonly object locker = new object();
    private readonly IDictionary<string, (Symbol Asset, Symbol Quote)> symbolMap = new Dictionary<string, (Symbol Asset, Symbol Quote)>();

    private long ping;
    private long Ping {
      get { lock (locker) { return ping; } }
      set { lock (locker) { ping = value; } }
    }

    public string Name => "Binance";
    public decimal CommissionFee => 0.00075m;
    public uint MaxMarketCountPerStream => 1024;

    public BinanceStreamAPIClient(string key, string secret) {
      this.secret = secret;
      client = new HttpClient();
      client.BaseAddress = new Uri(BaseUri);
      client.Timeout = TimeSpan.FromSeconds(10);
      client.DefaultRequestHeaders.Add("X-MBX-APIKEY", key);
    }

    public Task<bool> CancelOrder(IOrder order) => CancelOrder(order.Market, order.Id);

    public async Task<bool> CancelOrder(IMarket market, long orderId) {
      var res = await SendSigned("/api/v3/order", HttpMethod.Delete, new Dictionary<string, object>()
        { {"symbol", MarketString(market) },
        {"orderId", orderId.ToString()} });
      return res != null;
    }
      

    public async Task<OrderDTO?> CreateLimitOrder(IMarket market, OrderSide side, decimal price, decimal quantity) {
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
        return null;
      return CreateOrderDTO(market, response);
    }

    public async Task<OrderDTO?> CreateMarketOrder(IMarket market, OrderSide side, decimal quantity) {
      var response = await SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"side", side.ToString()},
        {"type", "MARKET"},
        {"quantity", quantity}
      });
      if (response == null || response.Id == -1)
        return null;
      return CreateOrderDTO(market, response);
    }

    public async Task<MarketDTO[]> FetchMarkets() {
      var response = await Send<ExchangeInformation>("/api/v3/exchangeInfo", HttpMethod.Get);
      var markets = new List<MarketDTO>();
      if (response != null) {
        Ping = response.ServerTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        symbolMap.Clear();
        var symbols = response.Symbols ?? Enumerable.Empty<SymbolInformation>();
        foreach (var symbol in symbols) {
          if (symbol.BaseAsset != null &&
              symbol.QuoteAsset != null &&
              symbol.Filters != null &&
              symbol.MarketString != null) {

            var assetPrecision = symbol.AssetPrecision;
            var quotePrecision = symbol.QuotePrecision;

            if (assetPrecision.HasValue && quotePrecision.HasValue) {
              var assetSymbol = new Symbol(symbol.BaseAsset);
              var quoteSymbol = new Symbol(symbol.QuoteAsset);
              symbolMap.Add($"{assetSymbol}{quoteSymbol}".ToUpper(), (assetSymbol, quoteSymbol));
              markets.Add(new MarketDTO() {
                Asset = assetSymbol,
                Quote = quoteSymbol,
                AssetPrecision = assetPrecision.Value,
                QuotePrecision = quotePrecision.Value
              });
            }
          }
        }
      }
      return markets.ToArray();
    }

    public async Task<OrderDTO?> GetOrder(IMarket market, long orderId) {
      var response = await SendSigned<BinanceOrder>("/api/v3/order", HttpMethod.Get, new Dictionary<string, object>()
      {
        {"symbol", MarketString(market) },
        {"orderId", orderId.ToString()}
      });
      if (response == null || response.Id != -1)
        return null;
      return CreateOrderDTO(market, response);


    }

    public async Task<WebSocket<ITradeUpdate>> GetStream(params IMarket[] markets) => await Task.Run(async () => {
      if (markets.Length == 0)
        throw new ArgumentException("Cannot start a stream with an empty market array");
      if (markets.Length > MaxMarketCountPerStream)
        throw new ArgumentException($"Cannot start a stream with more than {MaxMarketCountPerStream} markets.");

      var ws = new WebSocket<ITradeUpdate>(new Uri(WSBaseUri), WebSocketTransformFnc);
      await ws.SendMessage(
        "{\"method\": \"SUBSCRIBE\", \"params\": [" + 
            string.Join(",", markets.Select(x => $"\"{MarketString(x).ToLower()}@trade\"")) + 
         "], \"id\": 1}");
      return ws;
    });


    #region Helper
    private OrderDTO CreateOrderDTO(IMarket market, BinanceOrder order) {
      var state = OrderState.Open;
      if (order.Status != null) {
        state = order.Status.ToUpper() switch {
          "NEW" => OrderState.Open,
          "FILLED" => OrderState.Filled,
          "CANCELED" => OrderState.Canceled,
          _ => OrderState.Faulted
        };
      }

      decimal avgFillPrice = order.ExecutedQuantity > 0 ? 
        order.CummulativeQuoteQty / order.ExecutedQuantity : 0m;

      return new OrderDTO() {
        Id = order.Id,
        Market = market,
        AvgFillPrice = avgFillPrice,
        ExecutedQuantity = order.ExecutedQuantity,
        State = state
      };
    }

    private static string MarketString(IMarket market) => $"{market.QuoteAsset}{market.BaseAsset}".ToUpper();

    private ITradeUpdate? WebSocketTransformFnc(string msg) { // TODO: improve with Imarkets
      var update = JsonConvert.DeserializeObject<TradeStreamUpdate>(msg);
      if(update != null && update.Symbol != null && 
        symbolMap.TryGetValue(update.Symbol.ToUpper(), out (Symbol Asset, Symbol Quote) tpl)) {
        update.Asset = tpl.Asset;
        update.Quote = tpl.Quote;
        return update;
      }
      return null;
    }
    #endregion

    #region Http Client Abstraction

    
    #endregion
  }
}
