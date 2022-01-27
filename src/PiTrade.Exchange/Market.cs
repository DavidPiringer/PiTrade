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
    private readonly IList<Func<IMarket, ITradeUpdate, Task>> tradeUpdateFncs = new List<Func<IMarket, ITradeUpdate, Task>>();
    private readonly IList<Func<IMarket, decimal, Task>> priceChangedFncs = new List<Func<IMarket, decimal, Task>>();

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public decimal CurrentPrice { get; private set; }

    public bool IsEnabled { get; private set; }

    public Market(Exchange exchange, Symbol asset, Symbol quote, int assetPrecision, int quotePrecision) {
      name = $"{exchange}-{asset}{quote}";
      Exchange = exchange;
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
    }

    public async Task<(Order, ErrorState error)> CreateMarketOrder(OrderSide side, decimal quantity) {
      var qty = quantity.RoundDown(AssetPrecision);
      var price = CurrentPrice;
      long? orderId = null; 
      ErrorState error = ErrorState.NotConnected;
      if(IsEnabled) (orderId, error) = await NewMarketOrder(side, qty);
      return (new Order(orderId, this, side, price, qty), error);
    }
    public abstract Task<(long? orderId, ErrorState error)> NewMarketOrder(OrderSide side, decimal quantity);

    public async Task<(Order, ErrorState error)> CreateLimitOrder(OrderSide side, decimal price, decimal quantity) {
      var p = price.RoundDown(QuotePrecision);
      var qty = quantity.RoundDown(AssetPrecision);
      long? orderId = null;
      ErrorState error = ErrorState.NotConnected;
      if (IsEnabled) (orderId, error) = await NewLimitOrder(side, p, qty);
      return (new Order(orderId, this, side, price, qty), error);
    }
    public abstract Task<(long? orderId, ErrorState error)> NewLimitOrder(OrderSide side, decimal price, decimal quantity);
    //protected internal abstract Task<ErrorState> QueryCurrentOpenOrders(Order order);
    protected internal abstract Task<ErrorState> CancelOrder(Order order);
    protected abstract Task<ITradeUpdate?> MarketLoopCycle();


    public void Register2TradeUpdates(Func<IMarket, ITradeUpdate, Task> fnc) {
      Connect().Wait();
      tradeUpdateFncs.Add(fnc);
    }

    public void Unregister2TradeUpdates(Func<IMarket, ITradeUpdate, Task> fnc) {
      tradeUpdateFncs.Remove(fnc);
    }
     

    public void Register2PriceChanges(Func<IMarket, decimal, Task> fnc) {
      Connect().Wait();
      priceChangedFncs.Add(fnc);
    }

    public void Unregister2PriceChanges(Func<IMarket, decimal, Task> fnc) {
      priceChangedFncs.Remove(fnc);
    }

    protected internal virtual async Task Connect() {
      if (IsEnabled) return;
      IsEnabled = true;
      await Task.CompletedTask;
    }
    protected internal virtual async Task Disconnect() {
      if (!IsEnabled) return;
      IsEnabled = false;
      await Task.CompletedTask;
    }

    public override string ToString() => name;
    public override int GetHashCode() => name.GetHashCode();

    internal async Task Update() {
      if(!IsEnabled) return;

      var update = await MarketLoopCycle();
      if (update != null) {
        // update only if price has changed
        if (CurrentPrice != update.Price) {
          CurrentPrice = update.Price;
          foreach (var priceChangedFnc in priceChangedFncs.ToArray())
            await priceChangedFnc(this, update.Price);
        }
        foreach (var tradeUpdateFnc in tradeUpdateFncs.ToArray())
          await tradeUpdateFnc(this, update);
      }
    }
  }
}
