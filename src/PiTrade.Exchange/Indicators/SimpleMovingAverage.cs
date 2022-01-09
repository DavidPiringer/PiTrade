using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class SimpleMovingAverage : Indicator {

    public SimpleMovingAverage(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close)
      : base(period, maxTicks, indicatorValueType) { }

    protected override decimal Calculate(decimal value) =>
      (Value * (maxTicks - 1) + value) / maxTicks; 

  }
}
