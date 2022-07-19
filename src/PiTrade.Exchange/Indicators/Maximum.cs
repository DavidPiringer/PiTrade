using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class Maximum : Indicator {

    public Maximum(TimeSpan period, uint maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close)
      : base(period, maxTicks, indicatorValueType) { }

    protected override decimal Calculate(IEnumerable<decimal> values) => values.Max();
  }
}
