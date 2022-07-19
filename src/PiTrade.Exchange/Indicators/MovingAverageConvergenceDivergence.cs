using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  // https://www.investopedia.com/terms/m/macd.asp
  // not working currently
  public class MovingAverageConvergenceDivergence : Indicator {
    private readonly IIndicator ema26;
    private readonly IIndicator ema12;


    public MovingAverageConvergenceDivergence(TimeSpan period,
      IndicatorValueType indicatorValueType = IndicatorValueType.Close)
      : base(period, 26, indicatorValueType) { 
      ema26 = new ExponentialMovingAverage(period, 26, indicatorValueType);
      ema12 = new ExponentialMovingAverage(period, 12, indicatorValueType);
    }

    protected override decimal Calculate(IEnumerable<decimal> values) => ema12.Value - ema26.Value;
  }
}
