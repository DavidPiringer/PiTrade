using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Util {
  internal class PriceCandleTicker {
    private readonly TimeSpan Period;

    private DateTime lastPeriod = DateTime.Now;
    private IList<decimal> currentPeriod = new List<decimal>();

    public event Action<PriceCandle>? Tick;

    public PriceCandleTicker(TimeSpan updatePeriod) {
      this.Period = updatePeriod;
    }

    public void PriceUpdate(decimal price) {
      var lastPrice = currentPeriod.LastOrDefault(price);
      while (lastPeriod.Add(Period).CompareTo(DateTime.Now) <= 0) {
        if (currentPeriod.Count > 0) { // add last active candle
          Tick?.Invoke(new PriceCandle(currentPeriod.ToArray(), Period));
          currentPeriod.Clear();
        } else { // add inactive candles
          Tick?.Invoke(new PriceCandle(new decimal[] { lastPrice }, Period));
        }
        lastPeriod = lastPeriod.Add(Period);
      }
      if(lastPeriod.Add(Period).CompareTo(DateTime.Now) < 0) {
        lastPeriod = DateTime.Now;
      }

      currentPeriod.Add(price);

    }
  }
}
