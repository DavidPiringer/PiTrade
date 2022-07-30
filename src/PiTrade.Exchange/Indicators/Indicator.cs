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
    private readonly Queue<decimal> values;

    private DateTimeOffset lastPeriod;
    private IList<decimal> currentPeriod = new List<decimal>();
    private decimal lastPrice = decimal.MinValue;
    private uint tick = 0;

    public uint MaxTicks { get; }
    public bool IsReady { get; private set; }
    public TimeSpan Period { get; private set; }
    public decimal Value { get; private set; }
    public decimal ValuePreview { get; private set; }
    public decimal Variance { get; private set; }
    public decimal StandardDeviation { get; private set; }

    //private bool 

    public Indicator(TimeSpan period, uint maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close) {
      valueType = indicatorValueType;
      values = new Queue<decimal>((int)maxTicks);
      MaxTicks = maxTicks;
      Period = period;

      // round to last period 
      var tmp = DateTimeOffset.Now;
      var sub = tmp.ToUnixTimeSeconds() % ((long)period.TotalSeconds);
      lastPeriod = DateTimeOffset.FromUnixTimeSeconds(tmp.ToUnixTimeSeconds() - sub);
    }

    protected abstract decimal Calculate(IEnumerable<decimal> values);

    public void OnTrade(ITrade trade) => OnTrade(trade.Price, trade.UnixEpoch);
    public virtual void OnTrade(decimal value, long unixEpoch) {
      var offset = DateTimeOffset.FromUnixTimeMilliseconds(unixEpoch);

      // set lastPrice to price and lastEpoch to epoch for initialization
      if (lastPrice == decimal.MinValue) lastPrice = value;

      // throw error if an invalid (older) epoch is passed
      if (lastPeriod.CompareTo(offset) > 0) throw new ArgumentException("Cannot add older unixEpoch");
      
      // loop through periods if the last update is older than 'Period'
      Queue<PriceCandle> candles = new Queue<PriceCandle>();
      while (lastPeriod.Add(Period).CompareTo(offset) <= 0) {
        // if currentPeriod contains nothing -> add the lastPrice
        if (!currentPeriod.Any()) currentPeriod.Add(lastPrice);
        candles.Enqueue(new PriceCandle(currentPeriod.ToArray(), lastPeriod, Period));
        currentPeriod.Clear();
        lastPeriod = lastPeriod.Add(Period);
      }

      // dequeue candles
      while(candles.Count > 0)
        AddCandle(candles.Dequeue());

      // update lastPrice
      lastPrice = value;

      // add price to current period
      currentPeriod.Add(value);
      PreviewCandle(new PriceCandle(currentPeriod.ToArray(), lastPeriod.Add(Period), Period));
    }

    private decimal Aggregate(PriceCandle candle) =>
      valueType switch {
        IndicatorValueType.Average => candle.Average,
        IndicatorValueType.Open => candle.Open,
        IndicatorValueType.Close => candle.Close,
        IndicatorValueType.Min => candle.Min,
        IndicatorValueType.Max => candle.Max,
        IndicatorValueType.Typical => (candle.Min + candle.Max + candle.Close) / 3.0m,
        _ => candle.Average
      };

    private void AddCandle(PriceCandle candle) {
      var value = Aggregate(candle);
      if (IsReady) values.Dequeue();
      values.Enqueue(value);

      Value = tick == 0 ? value : Calculate(values);
      if (!IsReady) IsReady = (tick++) >= MaxTicks;
      // Calculate Variance and StdDev
      if (values.Count > 0) {
        var avg = values.Average();
        Variance = values.Sum(x => (x - avg) * (x - avg)) / values.Count;
        StandardDeviation = (decimal)Math.Sqrt((double)Variance);
      }
    }

    private void PreviewCandle(PriceCandle candle) {
      var value = Aggregate(candle);
      var tmpValues = new Queue<decimal>(values);
      if (IsReady) tmpValues.Dequeue();
      tmpValues.Enqueue(value);

      ValuePreview = tick == 0 ? value : Calculate(tmpValues);
    }
  }
}
