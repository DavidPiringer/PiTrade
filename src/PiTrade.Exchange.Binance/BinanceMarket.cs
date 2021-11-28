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

    //protected override async Task Listen(CancellationToken token) {
    //
    //  if (!WS.IsConnected)
    //    await Run(token);
    //}


    protected async override Task CancelOrder(Order order) {
        await Exchange.Cancel(order);
    }

    //public async override Task CancelAll() {
    //  if(ActiveOrders.Count() > 0) {
    //    await base.CancelAll();
    //    await Exchange.CancelAll(this);
    //  }
    //}


    protected override Task<Order> NewOrder(OrderSide side, decimal price, decimal quantity) =>
      Exchange.NewOrder(this, side, price, quantity);


    protected override async Task InitTradeLoop() {
      await WS.Connect(Uri);
    }

    protected override async Task<ITradeUpdate?> TradeUpdateLoopCycle(CancellationToken token) {
      while (!token.IsCancellationRequested) {
        string? msg = null;
        try {
          msg = await WS.NextMessage(); //TODO: nextmessage out param -> return bool (Success)?
        } catch (Exception ex) {
          Log.Error($"[OrderUpdateLoopCycle] -> {ex.Message}");
          continue;
        }

        var update = JsonConvert.DeserializeObject<TradeStreamUpdate>(msg); //TODO -> TradeStreamUpdate to BinanceTradeStreamUpdate?
        if (update == null) continue;
        else return update;
        /*
        await (PriceUpdate?.Invoke(update.Price) ?? Task.CompletedTask);
        var triggeredOrder = ActiveOrders.Where(x => x.Id == update.OIDBuyer || x.Id == update.OIDSeller).FirstOrDefault();
        if (triggeredOrder != null) {
          triggeredOrder.Fill(update.Quantity);
          switch (triggeredOrder.Side) {
            case OrderSide.BUY:
              OnBuy(triggeredOrder);
              //await (BuyOrderTriggered?.Invoke(triggeredOrder) ?? Task.CompletedTask);
              break;
            case OrderSide.SELL:
              OnSell(triggeredOrder);
              //await (SellOrderTriggered?.Invoke(triggeredOrder) ?? Task.CompletedTask);
              break;
          }
        }*/
      }
      return null;
    }

    protected override async Task ExitTradeLoop() {
      await WS.Disconnect();
    }

    // Generics -> Market<TradeTickCycle> -> liefert IOrderMatch oder so
    private async Task UpdateHandle(IMarketHandle handle, TradeStreamUpdate update) { //TODO -> move into Market/MarketHandle (zumindest Exchange assembly)
      var triggeredOrder = handle.ActiveOrders
        .Where(x => x.Id == update.OIDBuyer || x.Id == update.OIDSeller)
        .FirstOrDefault();

      if (triggeredOrder != null) {
        triggeredOrder.Fill(update.Quantity);
        switch (triggeredOrder.Side) {
          case OrderSide.BUY:
            OnBuy(triggeredOrder);
            //await (BuyOrderTriggered?.Invoke(triggeredOrder) ?? Task.CompletedTask);
            break;
          case OrderSide.SELL:
            OnSell(triggeredOrder);
            //await (SellOrderTriggered?.Invoke(triggeredOrder) ?? Task.CompletedTask);
            break;
        }
      }
    }
  }
}
