using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class ExponentialMovingAverage : Indicator {
    public readonly decimal smoothing;

    public ExponentialMovingAverage(IMarket market, TimeSpan period, uint maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close, decimal smoothing = 2m)
      : base(market, period, maxTicks, indicatorValueType) {
      this.smoothing = smoothing;
    }

    protected override decimal Calculate(decimal value) =>
      value * (smoothing / (MaxTicks + 1)) + Value * (1 - (smoothing / (MaxTicks + 1)));
    
      
  }
}
