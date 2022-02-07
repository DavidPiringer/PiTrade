﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Enums;

namespace PiTrade.Strategy.Util {
  public static class CommissionManager {
    private static readonly object locker = new object();
    public static IMarket? CommissionMarket { get; set; }
    public static decimal? CommissionFee { get; set; }
    public static decimal? BuyThreshold { get; set; }

    private static decimal Commission { get; set; }

    public static async Task<decimal?> ManageCommission(IOrder order) {
      decimal? quantity = null;
      decimal? commission = null;
      lock (locker) {
        if (CommissionMarket != null && CommissionFee.HasValue && BuyThreshold.HasValue) {
          commission = order.ExecutedAmount * CommissionFee.Value;
          Commission += commission.Value;
          if (Commission >= BuyThreshold.Value) {
            quantity = Commission / CommissionMarket.CurrentPrice;
            Commission = 0;
          }
        }
      }
      if (CommissionMarket != null && quantity.HasValue) { 
        await CommissionMarket.CreateMarketOrder(OrderSide.BUY, quantity.Value);
      }
      return commission;
    }
  }
}
