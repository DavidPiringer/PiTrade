﻿using System;
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
    private readonly object locker = new object();
    private readonly ConcurrentBag<Func<IIndicator, Task>> listeners = new ConcurrentBag<Func<IIndicator, Task>>();
    protected readonly IndicatorValueType valueType;
    protected readonly int maxTicks;

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

    public async Task Update(PriceCandle candle) {
      if(candle.Period.CompareTo(Period) == 0) {
        var isReady = false;
        lock(locker) { 
          var tmp = Value;
          Value = Tick == 0 ? candle.Average : Calculate(Aggregate(candle));
          var diff = (double)(Value - tmp);
          Slope = Math.Atan(diff / Period.TotalSeconds) * (180.0 / Math.PI);
          Tick = IsReady ? Tick : Tick + 1;
          isReady = IsReady;
        }
        // update listeners
        if (isReady)
          foreach (var listener in listeners)
            await listener(this);
      } else {
        Log.Error($"Candle has not the same period as referenced indicator.");
      }
    }

    public void Listen(Func<IIndicator, Task> fnc) => listeners.Add(fnc);

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
    
  }
}
