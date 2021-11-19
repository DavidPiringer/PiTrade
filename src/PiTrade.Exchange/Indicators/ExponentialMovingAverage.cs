using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class ExponentialMovingAverage : Indicator {
    public readonly decimal Smoothing;

    public ExponentialMovingAverage(TimeSpan period, IndicatorValueType indicatorValueType = IndicatorValueType.Average, int maxTicks = 100, decimal smoothing = 2m)
      : base(period, indicatorValueType, maxTicks) {
      Smoothing = smoothing;
    }

    protected override decimal Calculate(decimal value, decimal lastValue) =>
      (value * (Smoothing / (1 + Period.Seconds))) + lastValue * (1 - (Smoothing / (1 + Period.Seconds)));
  }
}
