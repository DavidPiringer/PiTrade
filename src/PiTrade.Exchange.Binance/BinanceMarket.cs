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
    private readonly WebSocket webSocket = new WebSocket();
    private readonly Uri uri;



    public new BinanceExchange Exchange { get; }

    internal BinanceMarket(BinanceExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision)
      : base(exchange, asset, quote, assetPrecision, quotePrecision) {
      Exchange = exchange;
      uri = new Uri($"wss://stream.binance.com:9443/ws/{$"{Asset}{Quote}".ToLower()}@trade");
    }

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
      await webSocket.Connect(uri);
    }

    protected override async Task<ITradeUpdate?> TradeUpdateLoopCycle(CancellationToken token) {
      try {
        var msg = await webSocket.NextMessage(); //TODO: nextmessage out param -> return bool (Success)?
        var update = JsonConvert.DeserializeObject<TradeStreamUpdate>(msg);
        return update;
      } catch (Exception ex) {
        Log.Error($"[OrderUpdateLoopCycle] -> {ex.Message}");
        return null;
      }
    }

    protected override async Task ExitTradeLoop() {
      await webSocket.Disconnect();
    }

  }
}
