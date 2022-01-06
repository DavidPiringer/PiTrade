﻿using Newtonsoft.Json;
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

    public override Task<(Order? order, ErrorState error)> NewMarketOrder(OrderSide side, decimal quantity) =>
      Exchange.NewMarketOrder(this, side, quantity);

    public override Task<(Order? order, ErrorState error)> NewLimitOrder(OrderSide side, decimal price, decimal quantity) =>
      Exchange.NewLimitOrder(this, side, price, quantity);

    protected async override Task<ErrorState> CancelOrder(Order order) =>
        await Exchange.Cancel(order);

    protected override async Task InitMarketLoop() {
      await webSocket.Connect(uri);
    }

    protected override async Task<ITradeUpdate?> MarketLoopCycle(CancellationToken token) {
      try {
        var msg = await webSocket.NextMessage(); //TODO: nextmessage out param -> return bool (Success)?
        if(msg == null) return null;

        return JsonConvert.DeserializeObject<TradeStreamUpdate>(msg);
      } catch (Exception ex) {
        Log.Error($"[OrderUpdateLoopCycle] -> {ex.Message}");
        return null;
      }
    }

    protected override async Task ExitMarketLoop() {
      await webSocket.Disconnect();
    }

    
  }
}
