using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Enums;

namespace PiTrade.Exchange.Indicators {
  public abstract class Indicator : IIndicator {
    protected readonly TimeSpan Period;
    protected readonly IndicatorValueType ValueType;
    protected readonly int MaxTicks;
    protected readonly decimal[] PeriodTicks;

    private DateTime LastTick { get; set; } = DateTime.Now;
    private IList<decimal> InPeriodTicks { get; } = new List<decimal>();
    private int First { get; set; } = 0;
    private int Last { get; set; } = 0;
    private bool IsFull => ((Last + 1) % MaxTicks) == First;

    public IEnumerable<decimal> Values {
      get {
        for(int i = First; i <= Last; i = (i + 1) % MaxTicks)
          yield return PeriodTicks[i];
      }
    }

    public Indicator(TimeSpan period, IndicatorValueType indicatorValueType = IndicatorValueType.Average, int maxTicks = 100) {
      Period = period;
      ValueType = indicatorValueType;
      MaxTicks = maxTicks;
      PeriodTicks = new decimal[MaxTicks];
    }

    public decimal Update(decimal value) { // ICandle
      if (LastTick.Add(Period).CompareTo(DateTime.Now) > 0) {
        // new Tick has started
        // buffer updated indexes to prevent wrong Value array (for Calculate method)
        var firstTmp = IsFull ? (First + 1) % MaxTicks : First;
        var lastTmp = (Last + 1) % MaxTicks;
        PeriodTicks[lastTmp] = Calculate(Aggregate(), PeriodTicks[Last]);
        First = firstTmp; 
        Last = lastTmp;
        InPeriodTicks.Clear();
      }
      InPeriodTicks.Add(value);
      LastTick = DateTime.Now;
      return PeriodTicks[Last];
    }

    protected abstract decimal Calculate(decimal value, decimal lastValue);

    private decimal Aggregate() {
      switch (ValueType) {
        case IndicatorValueType.Average: return InPeriodTicks.Average();
        case IndicatorValueType.Open: return InPeriodTicks.First();
        case IndicatorValueType.Close: return InPeriodTicks.Last();
        case IndicatorValueType.Min: return InPeriodTicks.Min();
        case IndicatorValueType.Max: return InPeriodTicks.Max();
        default: return InPeriodTicks.Average();
      }
    }
  }
}
