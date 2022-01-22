using System;
using System.Collections.Concurrent;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public abstract class Market : IMarket {
    private readonly string name;

    public event Action<IMarket, ITradeUpdate>? TradeUpdate;
    public event Action<IMarket, decimal>? PriceChanged;

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public decimal CurrentPrice { get; private set; }

    public Market(Exchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      name = $"{exchange}-{asset}{quote}";
      Exchange = exchange;
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
    }

    public Order CreateMarketOrder(OrderSide side, decimal quantity) {
      var qty = quantity.RoundDown(AssetPrecision);
      return new Order(NewMarketOrder(side, qty), this, side, CurrentPrice, qty);
    }
    public abstract Task<(long? orderId, ErrorState error)> NewMarketOrder(OrderSide side, decimal quantity);

    public Order CreateLimitOrder(OrderSide side, decimal price, decimal quantity) {
      var p = price.RoundDown(QuotePrecision);
      var qty = quantity.RoundDown(AssetPrecision);
      return new Order(NewLimitOrder(side, p, qty), this, side, p, qty);
    }
    public abstract Task<(long? orderId, ErrorState error)> NewLimitOrder(OrderSide side, decimal price, decimal quantity);

    protected internal abstract Task<ErrorState> CancelOrder(Order order);
    protected abstract Task<ITradeUpdate?> MarketLoopCycle();

    public override string ToString() => name;
    public override int GetHashCode() => name.GetHashCode();

    internal async Task Update() {
      var update = await MarketLoopCycle();
      if (update != null) {
        // update only if price has changed
        if (CurrentPrice != update.Price) {
          CurrentPrice = update.Price;
          PriceChanged?.Invoke(this, update.Price);
        }
        TradeUpdate?.Invoke(this, update);
      }
    }
  }
}
