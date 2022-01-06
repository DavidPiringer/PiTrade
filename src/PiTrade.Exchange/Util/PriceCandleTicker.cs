using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;

namespace PiTrade.Exchange.Util {
  internal sealed class PriceCandleTicker {
    private readonly object locker = new object();
    private readonly TimeSpan Period;
    private readonly ConcurrentBag<IIndicator> indicators = new ConcurrentBag<IIndicator>();
    private readonly ConcurrentBag<decimal> currentPeriod = new ConcurrentBag<decimal>();

    private DateTime lastPeriod = DateTime.Now;

    public PriceCandleTicker(TimeSpan updatePeriod) {
      this.Period = updatePeriod;
    }

    public void Listen(IIndicator indicator) => indicators.Add(indicator);

    public async Task PriceUpdate(decimal price) {
      while (lastPeriod.Add(Period).CompareTo(DateTime.Now) <= 0) {
        // update indicators
        foreach (var indicator in indicators)
          await indicator.Update(new PriceCandle(currentPeriod.ToArray(), Period));
        if (currentPeriod.Count > 0)
          currentPeriod.Clear();
        lastPeriod = lastPeriod.Add(Period);
      }
      lock(locker) { 
        if(lastPeriod.Add(Period).CompareTo(DateTime.Now) < 0)
          lastPeriod = DateTime.Now;
      }
      currentPeriod.Add(price);
    }
  }
}
