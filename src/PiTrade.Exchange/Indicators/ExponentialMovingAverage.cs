using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class ExponentialMovingAverage : Indicator {
    public readonly decimal smoothing;

    public ExponentialMovingAverage(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close, decimal smoothing = 2m, bool simulateWithFirstUpdate = false)
      : base(period, maxTicks, indicatorValueType, simulateWithFirstUpdate) {
      this.smoothing = smoothing;
    }

    protected override decimal Calculate(decimal value) =>
      value * (smoothing / (maxTicks + 1)) + Value * (1 - (smoothing / (maxTicks + 1)));
    
      
  }
}
