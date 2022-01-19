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
  public abstract class Strategy {

    public static IMarket? CommissionMarket { get; set; }

    protected const decimal CommissionFee = 0.00075m;

    protected IMarket Market { get; }
    protected decimal Expenses { get; private set; }
    protected decimal Returns { get; private set; }
    protected static decimal OpenCommission { get; private set; }


    private static readonly object locker = new object();
    private static decimal Profit { get; set; }


    protected Strategy(IMarket market) {
      this.Market = market;
      market.Listen(Update);
    }

    public virtual async Task Run() => await Market.Connect();

    protected abstract Task Update(decimal currentPrice);

    protected void AddFilledOrder(Order order) {
      decimal tmpCommission = 0;
      decimal commissionOfOrder = order.Amount * CommissionFee;
      if (order.IsFilled) {
        lock(locker) {
          OpenCommission += commissionOfOrder;
          tmpCommission = OpenCommission;
          if(tmpCommission > 15) {
            Expenses += tmpCommission;
            OpenCommission = 0;
          }
          if (order.Side == OrderSide.BUY) {
            Expenses += order.Amount;
            Expenses += commissionOfOrder;
          } else if (order.Side == OrderSide.SELL) {
            Returns += order.Amount;
            Expenses += commissionOfOrder;
            Profit += Returns - Expenses;
          }

          if (Profit < -20m) {
            EmergencyStop();
          }
        }
      }

      if(CommissionMarket != null && tmpCommission > 15) {
        var quantity = tmpCommission / CommissionMarket.CurrentPrice;
        CommissionMarket.CreateMarketOrder(OrderSide.BUY, quantity);
      }
    }

    protected virtual void EmergencyStop() {
      Log.Error("Closing Application because of great losses.");
    }

    protected virtual void Reset() {
      PrintStatus();
    }

    protected virtual void PrintStatus() {
      Log.Info($"Reset {Market.Asset}/{Market.Quote}");
      Log.Info($"Profit = {Profit}");
    }
  }
}
