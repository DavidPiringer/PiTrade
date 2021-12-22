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
    protected readonly IndicatorValueType valueType;
    protected readonly int maxTicks;
    //protected readonly PriceCandle[] periodTicks;

    private int Tick { get; set; } = 0;
    //private int First { get; set; } = 0;
    //private int Last { get; set; } = 0;
    //private bool IsEmpty => First == Last;
    public bool IsReady => Tick >= maxTicks;//((Last + 1) % maxTicks) == First;
    public TimeSpan Period { get; private set; }    
    public decimal Value { get; private set; }
    public double Slope { get; private set; }

    public Indicator(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close) {
      Period = period;
      valueType = indicatorValueType;
      this.maxTicks = maxTicks;
      //periodTicks = new PriceCandle[this.maxTicks];
    }

    public void Update(PriceCandle candle) {
      if(candle.Period.CompareTo(Period) == 0) {
        //First = IsReady ? (First + 1) % maxTicks : First;
        //Last = (Last + 1) % maxTicks;
        //periodTicks[Last] = candle;
        var tmp = Value;
        Value = Tick == 0 ? candle.Average : Calculate(Aggregate(candle));
        var diff = (double)(Value - tmp);
        Slope = Math.Atan(diff / Period.TotalSeconds) * (180.0 / Math.PI);
        Tick = IsReady ? Tick : Tick + 1;
      } else {
        Log.Error($"Candle has not the same period as referenced ticker.");
      }
    }

    protected abstract decimal Calculate(decimal value);

    private decimal Aggregate(PriceCandle candle) {
      switch (valueType) {
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
