using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;

namespace PiTrade.Exchange.Indicators {
  public abstract class Indicator : IIndicator {
    private readonly bool simulateWithFirstUpdate = false;
    protected readonly IndicatorValueType valueType;
    protected readonly int maxTicks; //TODO: public property
    protected readonly Queue<decimal> values;

    private int Tick { get; set; } = 0;
    public bool IsReady { get; private set; }
    public TimeSpan Period { get; private set; }
    public decimal Value { get; private set; }
    public double Slope { get; private set; }

    public Indicator(TimeSpan period, int maxTicks = 100, IndicatorValueType indicatorValueType = IndicatorValueType.Close, bool simulateWithFirstUpdate = false) {
      Period = period;
      valueType = indicatorValueType;
      this.maxTicks = maxTicks;
      this.simulateWithFirstUpdate = simulateWithFirstUpdate;
      values = new Queue<decimal>(maxTicks);
    }

    public void Update(params PriceCandle[] candles) {
      foreach (var candle in candles)
        AddCandle(candle);
      if (simulateWithFirstUpdate && candles.Any())
        Simulate(candles.Last());
    }


    protected abstract decimal Calculate(decimal value);

    private decimal Aggregate(PriceCandle candle) =>
      valueType switch {
        IndicatorValueType.Average => candle.Average,
        IndicatorValueType.Open => candle.Open,
        IndicatorValueType.Close => candle.Close,
        IndicatorValueType.Min => candle.Min,
        IndicatorValueType.Max => candle.Max,
        _ => candle.Average
      };

    private void Simulate(PriceCandle candle) { 
      while(!IsReady) AddCandle(candle);
    }


    private void AddCandle(PriceCandle candle) {
      if (candle.Period.CompareTo(Period) != 0) {
        Log.Error($"Candle has not the same period as referenced indicator.");
        return;
      }
      var value = Aggregate(candle);
      if (IsReady) values.Dequeue();
      values.Enqueue(value);

      var tmp = Value;
      Value = Tick == 0 ? value : Calculate(value);
      var diff = (double)(Value - tmp);
      Slope = Math.Atan(diff / Period.TotalSeconds) * (180.0 / Math.PI);
      if (!IsReady) IsReady = (Tick++) >= maxTicks;
    }
  }
}
