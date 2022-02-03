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
    private readonly IExchangeStreamClient api;
    private readonly string name;
    private readonly IList<Func<IMarket, ITradeUpdate, Task>> tradeUpdateFncs = new List<Func<IMarket, ITradeUpdate, Task>>();
    private readonly IList<Func<IMarket, decimal, Task>> priceChangedFncs = new List<Func<IMarket, decimal, Task>>();

    public IExchange Exchange { get; }
    public Symbol Asset { get; }
    public Symbol Quote { get; }
    public int AssetPrecision { get; }
    public int QuotePrecision { get; }
    public decimal CurrentPrice { get; private set; }

    internal Market(Exchange exchange, IExchangeStreamClient api, MarketDTO dto) {
      this.api = api;
      name = $"{api.Name}-{dto.Asset}{dto.Quote}";
      Exchange = exchange;
      Asset = dto.Asset;
      Quote = dto.Quote;
      AssetPrecision = dto.AssetPrecision;
      QuotePrecision = dto.QuotePrecision;
    }

    public async Task<IOrder> CreateMarketOrder(OrderSide side, decimal quantity) {
      var qty = quantity.RoundDown(AssetPrecision);
      var price = CurrentPrice;
      return await Order.Create(api, this, async () => {
        var dto = await api.CreateMarketOrder(this, side, qty);
        if (dto.HasValue) {
          var o = dto.Value;
          o.Market = this;
          o.Quantity = qty;
          o.TargetPrice = price;
          o.Side = side;
          return o;
        }
        return null;
      });
    }

    public async Task<IOrder> CreateLimitOrder(OrderSide side, decimal price, decimal quantity) {
      var p = price.RoundDown(QuotePrecision);
      var qty = quantity.RoundDown(AssetPrecision);
      return await Order.Create(api, this, async () => {
        var dto = await api.CreateLimitOrder(this, side, p, qty);
        if (dto.HasValue) {
          var o = dto.Value;
          o.Market = this;
          o.Quantity = qty;
          o.TargetPrice = p;
          o.Side = side;
          return o;
        }
        return null;
      });
    }

    public void Register2TradeUpdates(Func<IMarket, ITradeUpdate, Task> fnc) => tradeUpdateFncs.Add(fnc);
    public void Unregister2TradeUpdates(Func<IMarket, ITradeUpdate, Task> fnc) => tradeUpdateFncs.Remove(fnc);


    public void Register2PriceChanges(Func<IMarket, decimal, Task> fnc) => priceChangedFncs.Add(fnc);
    public void Unregister2PriceChanges(Func<IMarket, decimal, Task> fnc) => priceChangedFncs.Remove(fnc);

    public override string ToString() => name;
    public override int GetHashCode() => name.GetHashCode();

    internal async Task Update(ITradeUpdate update) {
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
