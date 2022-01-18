using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class Minimum : Indicator {

    public Minimum(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close, bool simulateWithFirstUpdate = false)
      : base(period, maxTicks, indicatorValueType, simulateWithFirstUpdate) { }

    protected override decimal Calculate(decimal value) => values.Min();
  }
}
