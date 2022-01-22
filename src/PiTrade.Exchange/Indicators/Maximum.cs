using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public class Maximum : Indicator {

    public Maximum(IMarket market, TimeSpan period, uint maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close)
      : base(market, period, maxTicks, indicatorValueType) { }

    protected override decimal Calculate(decimal value) => values.Max();
  }
}
