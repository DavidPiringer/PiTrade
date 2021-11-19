using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;

namespace PiTrade.Exchange.Indicators {
  public abstract class Indicator : IIndicator {
    protected readonly IndicatorValueType ValueType;
    protected readonly int MaxTicks;
    protected readonly PriceCandle[] PeriodTicks;

    private int First { get; set; } = 0;
    private int Last { get; set; } = 0;
    private bool IsEmpty => First == Last;
    public bool IsReady => ((Last + 1) % MaxTicks) == First;
    public TimeSpan Period { get; private set; }    
    public decimal Value { get; private set; }

    public Indicator(TimeSpan period, IndicatorValueType indicatorValueType = IndicatorValueType.Close, int maxTicks = 100) {
      Period = period;
      ValueType = indicatorValueType;
      MaxTicks = maxTicks;
      PeriodTicks = new PriceCandle[MaxTicks];
    }

    public void Update(PriceCandle candle) {
      if(candle.Period.CompareTo(Period) == 0) {
        First = IsReady ? (First + 1) % MaxTicks : First;
        Last = (Last + 1) % MaxTicks;
        PeriodTicks[Last] = candle;
          Value = IsReady ? 
            Calculate(Aggregate(candle), Value) : 
            PeriodTicks.Where(x => x != null).Average(x => Aggregate(x));
      } else {
        Log.Error($"Candle has not the same period as referenced ticker.");
      }
    }

    protected abstract decimal Calculate(decimal value, decimal lastValue);

    private decimal Aggregate(PriceCandle candle) {
      switch (ValueType) {
        case IndicatorValueType.Average: return candle.Average;
        case IndicatorValueType.Open: return candle.Open;
        case IndicatorValueType.Close: return candle.Close;
        case IndicatorValueType.Min: return candle.Min;
        case IndicatorValueType.Max: return candle.Max;
        default: return candle.Average;
      }
    }
  }
}
