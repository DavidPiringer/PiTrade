using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class ExponentialMovingAverage : Indicator {
    public readonly decimal smoothing;

    public ExponentialMovingAverage(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close, decimal smoothing = 2m)
      : base(period, maxTicks, indicatorValueType) {
      this.smoothing = smoothing;
    }

    protected override decimal Calculate(decimal value, decimal lastValue) =>
      (value * (smoothing / maxTicks)) 
      + lastValue * (1 - (smoothing / maxTicks));
  }
}
