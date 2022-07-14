using System;
using System.Collections.Concurrent;
using PiTrade.Exchange.DTOs;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Networking;

namespace PiTrade.Exchange.Base {
  public sealed class Market : IMarket {

    public IExchange Exchange { get; }
    public Symbol BaseAsset { get; }
    public Symbol QuoteAsset { get; }
    public int BaseAssetPrecision { get; }
    public int QuoteAssetPrecision { get; }
    public decimal CurrentPrice { get; private set; }

    public Market(IExchange exchange, Symbol baseAsset, Symbol quoteAsset, int baseAssetPrecision, int quoteAssetPrecision) {
      Exchange = exchange;
      BaseAsset = baseAsset;
      QuoteAsset = quoteAsset;
      BaseAssetPrecision = baseAssetPrecision;
      QuoteAssetPrecision = quoteAssetPrecision;
    }

    public IOrder Sell(decimal quantity) => new Order(this, OrderSide.SELL, quantity);

    public IOrder Buy(decimal quantity) => new Order(this, OrderSide.BUY, quantity);

    public void Subscribe(Action<ITrade> onTrade) => 
      Exchange.Subscribe(this, onTrade); 
    public void SubscribeAsync(Func<ITrade, Task> onTrade) => 
      Subscribe(t => { Task.Run(async () => await onTrade(t)); });

    public void Unsubscribe(Action<ITrade> onTrade) => 
      Exchange.Unsubscribe(this, onTrade);
    public void UnsubscribeAsync(Func<ITrade, Task> onTrade) =>
      Unsubscribe(t => { Task.Run(async () => await onTrade(t)); });

    public override string ToString() => $"{BaseAsset}{QuoteAsset}";

    

  }
}
