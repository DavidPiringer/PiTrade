using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Logging;

namespace PiTrade.Strategy.Util {
  internal static class ProfitCalculator {
    private static readonly object locker = new object();
    private static decimal Profit { get; set; }

    internal static void Add(decimal profit) {
      lock(locker) {
        Profit += profit;
      }
    }

    internal static void PrintStats() {
      lock(locker) {
        Log.Info($"Profit = {Profit}");
      }
    }
  }
}
