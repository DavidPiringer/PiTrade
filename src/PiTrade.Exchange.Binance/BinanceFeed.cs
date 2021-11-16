using Newtonsoft.Json.Linq;
using PiTrade.Exchange.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance
{
  //public class BinanceFeed : ExchangeFeed
  //{
  //  public override event OrderEvent? OnBuy;
  //  public override event OrderEvent? OnSell;
  //  public override event PriceUpdateEvent? OnPriceUpdate;

  //  private IExchange Exchange { get; }
  //  public BinanceFeed(IExchange exchange, Market market) 
  //    : base(new Uri($"wss://stream.binance.com:9443/ws/{market.ToString().ToLower()}@trade"))
  //  { 
  //    Exchange = exchange;
  //  }

  //  protected override async Task HandleMessage(string msg)
  //  {
  //    var json = JObject.Parse(msg);
  //    var price = json["p"]?.ToObject<decimal>();
  //    var quantity = json["q"]?.ToObject<decimal>();
  //    var oidBuyer = json["b"]?.ToObject<int>();
  //    var oidSeller = json["a"]?.ToObject<int>();

  //    foreach(var order in new List<Order>(Exchange.ActiveOrders))
  //    {
  //      if(order.Id == oidBuyer && quantity.HasValue)
  //      {
  //        order.Fill(quantity.Value);
  //        if (order.IsFilled)
  //        {
  //          var tmp = OnBuy?.Invoke(order);
  //          if (tmp != null) await tmp;
  //        }
          
  //      } else if (order.Id == oidSeller && quantity.HasValue)
  //      {
  //        order.Fill(quantity.Value);
  //        if(order.IsFilled)
  //        {
  //          var tmp = OnSell?.Invoke(order);
  //          if (tmp != null) await tmp;
  //        }
  //      }
  //    }

  //    if (price.HasValue)
  //    {
  //      var t = OnPriceUpdate?.Invoke(price.Value);
  //      if (t != null) await t;
  //    }
        
  //  }
  //}
}
