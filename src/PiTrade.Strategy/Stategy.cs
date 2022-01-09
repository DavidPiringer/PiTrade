using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Logging;

namespace PiTrade.Strategy {
  public abstract class Stategy {
    protected const decimal CommissionFee = 0.00075m;

    protected IMarket Market { get; }
    protected decimal Quantity { get; private set; }
    protected decimal Revenue { get; private set; }
    protected static decimal Commission { get; private set; }


    private static readonly object locker = new object();
    private static decimal Profit { get; set; }


    protected Stategy(IMarket market) {
      this.Market = market;
    }

    public virtual async Task Run() => await Market.Connect();


    protected void AddFilledOrder(Order order) {
      if(order.IsFilled) {
        lock(locker) {
          Commission += order.Amount * CommissionFee;
          if (order.Side == OrderSide.BUY) {
            Revenue -= order.Amount;
            Quantity += order.Quantity;
          } else if (order.Side == OrderSide.SELL) {
            Revenue += order.Amount;
            Quantity -= order.Quantity;
            Profit += Revenue - Commission;
          }
          Quantity = Quantity.RoundDown(Market.AssetPrecision);
        }
      }
    }

    protected virtual void Reset() {
      lock(locker) {
        Quantity = 0;
        Revenue = 0;
      }
    }

    protected virtual void PrintStatus() {
      Log.Info($"Profit = {Profit}");
      Log.Info($"Commission = {Commission}");
    }
  }
}
