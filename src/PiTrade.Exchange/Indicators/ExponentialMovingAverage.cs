using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class ExponentialMovingAverage : Indicator {
    public readonly decimal Smoothing;

    public ExponentialMovingAverage(TimeSpan period, IndicatorValueType indicatorValueType = IndicatorValueType.Close, int maxTicks = 100, decimal smoothing = 2m)
      : base(period, indicatorValueType, maxTicks) {
      Smoothing = smoothing;
    }

    protected override decimal Calculate(decimal value, decimal lastValue) =>
      (value * (Smoothing / MaxTicks)) 
      + lastValue * (1 - (Smoothing / MaxTicks));
  }
}
