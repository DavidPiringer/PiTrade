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
    public Symbol QuoteAsset { get; }
    public Symbol BaseAsset { get; }
    public int QuoteAssetPrecision { get; }
    public int BaseAssetPrecision { get; }
    public decimal CurrentPrice { get; private set; }

    public Market(IExchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      Exchange = exchange;
      QuoteAsset = asset;
      BaseAsset = quote;
      QuoteAssetPrecision = assetPrecision;
      BaseAssetPrecision = quotePrecision;
    }

    public IOrder Sell(decimal quantity) => new Order(this, OrderSide.SELL, quantity);

    public IOrder Buy(decimal quantity) => new Order(this, OrderSide.BUY, quantity);

    public override string ToString() => $"{QuoteAsset}{BaseAsset}";
  }
}
