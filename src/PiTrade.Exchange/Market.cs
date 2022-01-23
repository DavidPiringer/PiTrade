using System;
using System.Collections.Concurrent;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Networking;

namespace PiTrade.Exchange {
  public abstract class Market : IMarket {
    private readonly string name;

    private event Action<IMarket, ITradeUpdate>? _TradeUpdate;
    public event Action<IMarket, ITradeUpdate>? TradeUpdate {
      add { Connect(); _TradeUpdate += value; }
      remove { Disconnect(); _TradeUpdate -= value; }
    }

    private event Action<IMarket, decimal>? _PriceChanged;
    public event Action<IMarket, decimal>? PriceChanged {
      add { Connect(); _PriceChanged += value; }
      remove { Disconnect(); _PriceChanged -= value; }
    }

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public decimal CurrentPrice { get; private set; }

    public bool IsEnabled { get; private set; }
      //(TradeUpdate != null && TradeUpdate.GetInvocationList().Length > 0) || 
      //(PriceChanged != null && PriceChanged.GetInvocationList().Length > 0); 

    public Market(Exchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      name = $"{exchange}-{asset}{quote}";
      Exchange = exchange;
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
    }

    public Order CreateMarketOrder(OrderSide side, decimal quantity) {
      Connect();
      var qty = quantity.RoundDown(AssetPrecision);
      return new Order(NewMarketOrder(side, qty), this, side, CurrentPrice, qty);
    }
    public abstract Task<(long? orderId, ErrorState error)> NewMarketOrder(OrderSide side, decimal quantity);

    public Order CreateLimitOrder(OrderSide side, decimal price, decimal quantity) {
      Connect();
      var p = price.RoundDown(QuotePrecision);
      var qty = quantity.RoundDown(AssetPrecision);
      return new Order(NewLimitOrder(side, p, qty), this, side, p, qty);
    }
    public abstract Task<(long? orderId, ErrorState error)> NewLimitOrder(OrderSide side, decimal price, decimal quantity);

    protected internal abstract Task<ErrorState> CancelOrder(Order order);
    protected abstract Task<ITradeUpdate?> MarketLoopCycle();

    protected virtual void Connect() {
      if (IsEnabled) return;
      IsEnabled = true;
    }
    protected virtual void Disconnect() {
      if (!IsEnabled && _PriceChanged != null && 
        _PriceChanged.GetInvocationList().Length > 0) 
        return;

      IsEnabled = false;
    }

    public override string ToString() => name;
    public override int GetHashCode() => name.GetHashCode();

    internal async Task Update() {
      if(!IsEnabled) return;
      Log.Info(name);
      var update = await MarketLoopCycle();
      if (update != null) {
        // update only if price has changed
        if (CurrentPrice != update.Price) {
          CurrentPrice = update.Price;
          _PriceChanged?.Invoke(this, update.Price);
        }
        _TradeUpdate?.Invoke(this, update);
      }
    }
  }
}
