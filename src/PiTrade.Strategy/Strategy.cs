using System;
using System.Collections.Concurrent;
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
    private static readonly object locker = new object();
    private static ConcurrentDictionary<int, Order> orders = new ConcurrentDictionary<int, Order>();

    protected const decimal CommissionFee = 0.00075m;

    public static IMarket? CommissionMarket { get; set; }


    protected IMarket Market { get; }

    protected static decimal OpenCommission { get; private set; }

    private static decimal profit = 0m;
    private static decimal Profit {
      get => profit;
      set { lock (locker) profit = value; }
    }


    protected Strategy(IMarket market) {
      this.Market = market;
    }

    //protected abstract Task Update(decimal currentPrice);

    protected async Task AddFilledOrder(Order order) {
      //orders.TryAdd(order.GetHashCode(), order);

      decimal tmpCommission = 0;
      decimal commissionOfOrder = order.Amount * CommissionFee;
      if (order.IsFilled) {
        lock(locker) {
          OpenCommission += commissionOfOrder;
          tmpCommission = OpenCommission;
          if(tmpCommission > 15) {
            OpenCommission = 0;
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
    }
  }
}
