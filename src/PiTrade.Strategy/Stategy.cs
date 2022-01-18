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

    public static IMarket? CommissionMarket { get; set; }

    protected const decimal CommissionFee = 0.00075m;

    protected IMarket Market { get; }
    protected static decimal Expenses { get; private set; }
    protected static decimal Returns { get; private set; }
    protected static decimal Commission { get; private set; }


    private static readonly object locker = new object();
    private static decimal Profit { get; set; }


    protected Stategy(IMarket market) {
      this.Market = market;
    }

    public virtual async Task Run() => await Market.Connect();


    protected void AddFilledOrder(Order order) {
      decimal tmpCommission = 0;
      if(order.IsFilled) {
        lock(locker) {
          Commission += order.Amount * CommissionFee;
          tmpCommission = Commission;
          if(tmpCommission > 15) {
            Expenses += tmpCommission;
            Commission = 0;
          }
          if (order.Side == OrderSide.BUY)
            Expenses += order.Amount;
          else if (order.Side == OrderSide.SELL)
            Returns += order.Amount;
          Profit = Returns - Expenses - Commission;

          if (Profit < -10) {
            Log.Error("Closing Application because of great losses.");
            Environment.Exit(-1);
          }
        }
      }

      if(CommissionMarket != null && tmpCommission > 15) {
        var quantity = tmpCommission / CommissionMarket.CurrentPrice;
        CommissionMarket.CreateMarketOrder(OrderSide.BUY, quantity);
      }
    }

    protected virtual void Reset() { }

    protected virtual void PrintStatus() {
      Log.Info($"Profit = {Profit}");
      Log.Info($"Commission = {Commission}");
    }
  }
}
