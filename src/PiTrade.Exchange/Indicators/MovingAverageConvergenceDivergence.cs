using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  // https://www.investopedia.com/terms/m/macd.asp
  // Value is the macd line
  public class MovingAverageConvergenceDivergence : Indicator {
    private readonly IIndicator slow;
    private readonly IIndicator fast;
    private readonly IIndicator signal;

    public decimal FastValue => fast.Value;
    public decimal SlowValue => slow.Value;
    public decimal Signal => signal.Value;
    public bool IsUptrend => slow.IsReady && fast.IsReady && signal.IsReady && Value > Signal;

    public MovingAverageConvergenceDivergence(TimeSpan period, uint slowMaxTicks = 26, uint fastMaxTicks = 12, uint signalMaxTicks = 7, IndicatorValueType indicatorValueType = IndicatorValueType.Typical)
      : base(period, slowMaxTicks, indicatorValueType) {
      slow = new ExponentialMovingAverage(period, slowMaxTicks, indicatorValueType);
      fast = new ExponentialMovingAverage(period, fastMaxTicks, indicatorValueType);
      signal = new SimpleMovingAverage(period, signalMaxTicks, indicatorValueType);
    }

    public override void Add(decimal value, long unixEpoch) {
      slow.Add(value, unixEpoch);
      fast.Add(value, unixEpoch);
      if(slow.IsReady && fast.IsReady) {
        base.Add(fast.Value - slow.Value, unixEpoch);
        signal.Add(Value, unixEpoch);
      }
    }

    protected override decimal Calculate(IEnumerable<decimal> values) => values.LastOrDefault();
  }
}
