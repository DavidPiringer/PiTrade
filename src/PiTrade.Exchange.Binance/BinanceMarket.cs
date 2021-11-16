using Newtonsoft.Json;
using PiTrade.Exchange.BasesClasses;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.Entities;
using PiTrade.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance
{
  internal class BinanceMarket : Market
  {
    private readonly WebSocket WS = new WebSocket();
    private readonly Uri Uri;

    public new BinanceExchange Exchange { get; }

    internal BinanceMarket(BinanceExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) 
      : base(exchange, asset, quote, assetPrecision, quotePrecision) {
      Exchange = exchange;
      Uri = new Uri($"wss://stream.binance.com:9443/ws/{$"{Asset}{Quote}".ToLower()}@trade");
    }

    public override async Task Listen(Func<Order, Task> onBuy, Func<Order, Task> onSell, Func<decimal, Task> onPriceUpdate, CancellationToken token)
    {
      await WS.Connect(Uri);
      await await Task.Factory.StartNew(async () => {
        while (!token.IsCancellationRequested)
        {
          var msg = await WS.NextMessage();
          var update = JsonConvert.DeserializeObject<TradeStreamUpdate>(msg);
          if (update == null) continue;

          var triggeredOrder = ActiveOrders.Where(x => x.Id == update.OIDBuyer || x.Id == update.OIDSeller).FirstOrDefault();
          if(triggeredOrder != null)
          {
            triggeredOrder.Fill(update.Quantity);
            switch(triggeredOrder.Side)
            {
              case OrderSide.BUY:
                await onBuy(triggeredOrder);
                break;
              case OrderSide.SELL:
                await onSell(triggeredOrder);
                break;
            }
          }
          await onPriceUpdate(update.Price);
        }
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

      await WS.Disconnect();
    }


    public async override Task Cancel(Order order)
    {
      await base.Cancel(order);
      await Exchange.Cancel(order);
    }

    public async override Task CancelAll()
    {
      await base.CancelAll();
      await Exchange.CancelAll(this);
    }


    public override Task<Order> NewOrder(OrderSide side, decimal price, decimal quantity) => 
      Exchange.NewOrder(this, side, price, quantity);


  }
}
