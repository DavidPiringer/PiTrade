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
    protected readonly IList<Func<IIndicator, Task>> listeners = new List<Func<IIndicator, Task>>();

    private int Tick { get; set; } = 0;
    private bool IsReady => Tick >= maxTicks;
    public TimeSpan Period { get; private set; }    
    public decimal Value { get; private set; }
    public double Slope { get; private set; }

    public Indicator(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close) {
      Period = period;
      valueType = indicatorValueType;
      this.maxTicks = maxTicks;
    }

    public void Update(PriceCandle candle) {
      if(candle.Period.CompareTo(Period) == 0) {
        var tmp = Value;
        Value = Tick == 0 ? candle.Average : Calculate(Aggregate(candle));
        var diff = (double)(Value - tmp);
        Slope = Math.Atan(diff / Period.TotalSeconds) * (180.0 / Math.PI);
        Tick = IsReady ? Tick : Tick + 1;

        // update listeners
        if (IsReady)
          foreach (var listener in listeners)
            listener(this);
      } else {
        Log.Error($"Candle has not the same period as referenced ticker.");
      }
    }

    void Register(Func<IIndicator, Task> fnc) => listeners.Add(fnc);
    void Unregister(Func<IIndicator, Task> fnc) => listeners.Remove(fnc);

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
