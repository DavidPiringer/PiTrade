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
    private decimal lastPrice = decimal.MinValue;

    public PriceCandleTicker(TimeSpan updatePeriod) {
      this.Period = updatePeriod;
    }

    public void Listen(IIndicator indicator) => indicators.Add(indicator);

    public async Task PriceUpdate(decimal price) {
      // set lastPrice to price for initialization
      if(lastPrice == decimal.MinValue) lastPrice = price;

      var candles = FetchCandles();

      // update indicators
      if(candles.Any()) foreach (var indicator in indicators)
        await indicator.Update(candles.ToArray());

      // update periods and lastPrice
      lock (locker) { 
        if(lastPeriod.Add(Period).CompareTo(DateTime.Now) < 0)
          lastPeriod = DateTime.Now;
        lastPrice = price;
      }

      currentPeriod.Add(price);
    }

    private IEnumerable<PriceCandle> FetchCandles() {
      // loop through periods if the last update is older than 'Period'
      IList<PriceCandle> candles = new List<PriceCandle>();
      while (lastPeriod.Add(Period).CompareTo(DateTime.Now) <= 0) {
        // if currentPeriod contains nothing -> add the lastPrice
        if (!currentPeriod.Any()) currentPeriod.Add(lastPrice);
        candles.Add(new PriceCandle(currentPeriod.ToArray(), Period));
        if (currentPeriod.Count > 0)
          currentPeriod.Clear();
        lastPeriod = lastPeriod.Add(Period);
      }
      return candles;
    }
  }
}
