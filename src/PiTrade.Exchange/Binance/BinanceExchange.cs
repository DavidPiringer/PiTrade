using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;


namespace PiTrade.Exchange.Binance
{
  public sealed class BinanceExchange : IExchange
  {
    private readonly BinanceHttpWrapper BinanceHttpWrapper;


    private IList<Order> activeOrders = new List<Order>();
    public IEnumerable<Order> ActiveOrders => activeOrders;
    

    // TODO
    public IEnumerable<Market> AvailableMarkets { get; } = Enumerable.Empty<Market>();

    private static readonly object locker = new object();

    public BinanceExchange(string key, string secret)
    {
      BinanceHttpWrapper = new BinanceHttpWrapper(key, secret);
    }

    public async Task<Order> Get(int id)
    {
      await Task.CompletedTask;
      var order = ActiveOrders.Where(x => x.Id == id).FirstOrDefault();
      if (order == null)
        throw new Exception($"Order '{order}' is not active.");
      return order;
    }

    public async Task<Order> Buy(Market market, decimal price, decimal quantity)
    {
      var order = await NewOrder(OrderSide.BUY, market,
        price.RoundDown(market.QuotePrecision),
        quantity.RoundUp(market.AssetPrecision));
      lock(locker)
        activeOrders.Add(order);
      return order;
    }

    public async Task<Order> Sell(Market market, decimal price, decimal quantity)
    {
      var order = await NewOrder(OrderSide.SELL, market, 
        price.RoundUp(market.QuotePrecision), 
        quantity.RoundDown(market.AssetPrecision));
      lock (locker)
        activeOrders.Add(order);
      return order;
    }

    public async Task Cancel(Order order)
    {
      await BinanceHttpWrapper.SendSigned("/api/v3/order", HttpMethod.Delete, new Dictionary<string, object>()
      { {"symbol", order.Market.ToString()},
        {"orderId", order.Id.ToString()} });
      lock (locker) // TODO: rework
        activeOrders = ActiveOrders.Where(x => x.Id != order.Id).ToList();
    }
      

    public async Task CancelAll(Market market)
    {
      await BinanceHttpWrapper.SendSigned("/api/v3/openOrders", HttpMethod.Delete, new Dictionary<string, object>()
      { {"symbol", market.ToString()} });
      lock (locker)
        activeOrders.Clear();
    }
      

    public IExchangeFeed GetFeed(Market market) => new BinanceFeed(this, market);


    private async Task<Order> NewOrder(OrderSide side, Market market, decimal price, decimal quantity)
    {
      var response = await BinanceHttpWrapper.SendSigned("/api/v3/order", HttpMethod.Post, new Dictionary<string, object>()
      {
        {"symbol", market.ToString()},
        {"side", side.ToString()},
        {"type", "LIMIT"},
        {"timeInForce", "GTC"},
        {"quantity", quantity},
        {"price", price}
      });
      var json = JObject.Parse(response);
      var id = json["orderId"]?.ToObject<int>();
      if (!id.HasValue)
        throw new Exception($"Response contains no order id ({response})");
      return new Order(id.Value, market, side, price, quantity);
    }

    
    public async Task GetExchangeInformation(Market market) //TODO: -> create all markets?
    {
      var response = await BinanceHttpWrapper.Send("/api/v3/exchangeInfo", HttpMethod.Get, new Dictionary<string, object>() 
      { { "symbol", market.ToString()} });
      
      var obj = JObject.Parse(response);
      var serverTime = obj["serverTime"]?.ToObject<long>();
      if (serverTime.HasValue) // TODO: aufhübschen -> in Wrapper rein? -> init exchange with this info?
        BinanceHttpWrapper.ServerTimeDelta = serverTime.Value - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

      //BinanceHttpWrapper
      /*var symbolInfo = obj["symbols"]?.Where(x => x["symbol"]?.ToString() == market.ToString()).First();
      if (symbolInfo != null)
      {
        var priceFilter = symbolInfo["filters"]?.Where(x => x["filterType"]?.ToString() == "PRICE_FILTER").First();
        var lotSize = symbolInfo["filters"]?.Where(x => x["filterType"]?.ToString() == "LOT_SIZE").First();
      }*/
      Console.WriteLine(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
      Console.WriteLine(response);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetFunds()
    {
      var response = await BinanceHttpWrapper.SendSigned("/api/v3/account", HttpMethod.Get);
      var balances = JObject.Parse(response)["balances"]?.ToArray();
      Dictionary<string,decimal> funds = new Dictionary<string, decimal>();
      if(balances != null)
      {
        foreach(var balance in balances)
        {
          var symStr = balance["asset"]?.ToString();
          var qty = balance["free"]?.ToObject<decimal>();
          if(symStr != null && qty.HasValue)
            funds.Add(symStr.Trim().ToUpper(), qty.Value);
        }
      }
      return funds;
    }
  }
}
