using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;

namespace PiTrade.Strategy.Util {
  public class CommisionManager {
    private static readonly object locker = new object();
    public static IMarket? Market { get; set; }
    public static decimal Threshold { get; set; } = 15m;
    private static decimal Commission { get; set; }


    public static async Task Add(decimal commission) {
      lock(locker) {
        Commission += commission;
      }
      if(Commission >= Threshold && Market != null) {
        var price = Market.CurrentPrice;
        await Market.Buy(price, (Commission / price));
        lock(locker) {
          Commission = 0m;
        }
      }
    }
  }
}
