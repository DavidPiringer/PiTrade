using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class PriceCandle {
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public virtual decimal Open { get; }
    public virtual decimal Close { get; }
    public virtual decimal Max { get; }
    public virtual decimal Min { get; }
    public decimal Typical => (Min + Max + Close) / 3.0m;

    public PriceCandle(decimal[] values, DateTimeOffset start, DateTimeOffset end) {
      if (values == null || values.Length == 0)
        throw new ArgumentException("The value array of an price candle cannot be null or empty.");
      Open = values.First();
      Close = values.Last();
      Max = values.Max();
      Min = values.Min();
      Start = start;
      End = end;
    }
  }
}
