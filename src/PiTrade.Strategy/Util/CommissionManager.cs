using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;

namespace PiTrade.Strategy.Util {

  // TODO: Refactor this class
  public class CommissionManager {
    private static readonly object locker = new object();
    private static IMarket? market;
    public static IMarket? Market {
      get => market;
      set {
        lock (locker) {
          market = value;
          if(Market != null) {
            MarketHandle = Market.GetMarketHandle(out Task awaitTask);
            AwaitTask = awaitTask;
          }
            
        }
      }
    }

    public static decimal Threshold { get; set; } = 15m;
    private static decimal Commission { get; set; }
    public static IMarketHandle? MarketHandle { get; set; }
    public static Task? AwaitTask { get; set; }


    public static async Task Add(decimal commission) {
      lock(locker) {
        Commission += commission;
      }
      if(Commission >= Threshold && Market != null && MarketHandle != null) {
        var price = Market.CurrentPrice;
        await MarketHandle.BuyLimit(price, (Commission / price));
        lock(locker) {
          Commission = 0m;
        }
      }
    }
  }
}
