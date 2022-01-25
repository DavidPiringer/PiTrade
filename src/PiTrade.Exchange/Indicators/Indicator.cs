using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public abstract class Indicator : IIndicator {
    protected readonly IndicatorValueType valueType;
    protected readonly Queue<decimal> values;

    private DateTime lastPeriod = DateTime.Now;
    private IList<decimal> currentPeriod = new List<decimal>();
    private decimal lastPrice = decimal.MinValue;
    private uint tick = 0;

    public uint MaxTicks { get; }
    public bool IsReady { get; private set; }
    public TimeSpan Period { get; private set; }
    public decimal Value { get; private set; }

    public Indicator(IMarket market, TimeSpan period, uint maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close) {
      valueType = indicatorValueType;
      values = new Queue<decimal>((int)maxTicks);
      MaxTicks = maxTicks;
      Period = period;

      market.Register2PriceChanges(OnPriceUpdate);
    }

    protected abstract decimal Calculate(decimal value);

    private async Task OnPriceUpdate(IMarket market, decimal price) {
      // set lastPrice to price for initialization
      if (lastPrice == decimal.MinValue) lastPrice = price;

      // loop through periods if the last update is older than 'Period'
      Queue<PriceCandle> candles = new Queue<PriceCandle>();
      while (lastPeriod.Add(Period).CompareTo(DateTime.Now) <= 0) {
        // if currentPeriod contains nothing -> add the lastPrice
        if (!currentPeriod.Any()) currentPeriod.Add(lastPrice);
        candles.Enqueue(new PriceCandle(currentPeriod.ToArray(), lastPeriod, Period));
        currentPeriod.Clear();
        lastPeriod = lastPeriod.Add(Period);
      }

      // dequeue candles
      while(candles.Count > 0)
        AddCandle(candles.Dequeue());

      // update periods and lastPrice
      if (lastPeriod.Add(Period).CompareTo(DateTime.Now) < 0)
        lastPeriod = DateTime.Now;
      lastPrice = price;

      // add price to current period
      currentPeriod.Add(price);

      await Task.CompletedTask;
    }

    private decimal Aggregate(PriceCandle candle) =>
      valueType switch {
        IndicatorValueType.Average => candle.Average,
        IndicatorValueType.Open => candle.Open,
        IndicatorValueType.Close => candle.Close,
        IndicatorValueType.Min => candle.Min,
        IndicatorValueType.Max => candle.Max,
        _ => candle.Average
      };

    private void AddCandle(PriceCandle candle) {
      var value = Aggregate(candle);
      if (IsReady) values.Dequeue();
      values.Enqueue(value);

      Value = tick == 0 ? value : Calculate(value);
      if (!IsReady) IsReady = (tick++) >= MaxTicks;
    }
  }
}
