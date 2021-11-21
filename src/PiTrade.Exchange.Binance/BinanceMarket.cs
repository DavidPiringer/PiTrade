using Newtonsoft.Json;
using PiTrade.Exchange.Binance.Domain;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;
using PiTrade.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance {
  internal class BinanceMarket : Market {
    private readonly WebSocket WS = new WebSocket();
    private readonly Uri Uri;



    public new BinanceExchange Exchange { get; }

    internal BinanceMarket(BinanceExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision)
      : base(exchange, asset, quote, assetPrecision, quotePrecision) {
      Exchange = exchange;
      Uri = new Uri($"wss://stream.binance.com:9443/ws/{$"{Asset}{Quote}".ToLower()}@trade");
    }

    protected override async Task Listen(CancellationToken token) {

      if (!WS.IsConnected)
        await Run(token);
    }


    public async override Task Cancel(Order order) {
      if(ActiveOrders.Any(x => x.Id == order.Id)) {
        await base.Cancel(order);
        await Exchange.Cancel(order);
      }
    }

    public async override Task CancelAll() {
      if(ActiveOrders.Count() > 0) {
        await base.CancelAll();
        await Exchange.CancelAll(this);
      }
    }


    public override Task<Order> NewOrder(OrderSide side, decimal price, decimal quantity) =>
      Exchange.NewOrder(this, side, price, quantity);


    private async Task Run(CancellationToken token) {
      await await Task.Factory.StartNew(async () => {
        await WS.Connect(Uri);
        while (!token.IsCancellationRequested) {
          var msg = await WS.NextMessage();
          var update = JsonConvert.DeserializeObject<TradeStreamUpdate>(msg);
          if (update == null) continue;

          CurrentPrice = update.Price;
          try {
            await (PriceUpdate?.Invoke(update.Price) ?? Task.CompletedTask);
            var triggeredOrder = ActiveOrders.Where(x => x.Id == update.OIDBuyer || x.Id == update.OIDSeller).FirstOrDefault();
            if (triggeredOrder != null) {
              triggeredOrder.Fill(update.Quantity);
              switch (triggeredOrder.Side) {
                case OrderSide.BUY:
                  await (BuyOrderTriggered?.Invoke(triggeredOrder) ?? Task.CompletedTask);
                  break;
                case OrderSide.SELL:
                  await (SellOrderTriggered?.Invoke(triggeredOrder) ?? Task.CompletedTask);
                  break;
              }
            }
          } catch (Exception ex) { 
            Log.Error($"[IN MARKET RUN] -> {ex.Message}");
          }
        }
        await WS.Disconnect();
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
  }
}
