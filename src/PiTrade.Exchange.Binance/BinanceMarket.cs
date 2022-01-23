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
    private readonly WebSocket webSocket;



    public new BinanceExchange Exchange { get; }

    internal BinanceMarket(BinanceExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision)
      : base(exchange, asset, quote, assetPrecision, quotePrecision) {
      Exchange = exchange;
      webSocket = new WebSocket(new Uri($"wss://stream.binance.com:9443/ws/{$"{Asset}{Quote}".ToLower()}@trade"));
    }

    public override Task<(long? orderId, ErrorState error)> NewMarketOrder(OrderSide side, decimal quantity) =>
      Exchange.NewMarketOrder(this, side, quantity);

    public override Task<(long? orderId, ErrorState error)> NewLimitOrder(OrderSide side, decimal price, decimal quantity) =>
      Exchange.NewLimitOrder(this, side, price, quantity);

    protected async override Task<ErrorState> CancelOrder(Order order) =>
        await Exchange.Cancel(order);

    protected override void Connect() {
      base.Connect();
      webSocket.Connect().Wait();
    }

    protected override void Disconnect() {
      base.Disconnect();
      webSocket.Disconnect().Wait();
    }

    protected override async Task<ITradeUpdate?> MarketLoopCycle() {
      (string? msg, bool success) = await webSocket.NextMessage();
      if (success && msg != null)
        return JsonConvert.DeserializeObject<TradeStreamUpdate>(msg);
      return null;
    }

  }
}
