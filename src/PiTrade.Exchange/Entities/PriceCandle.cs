using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class PriceCandle {
    public DateTimeOffset DateTimeOffset { get; }
    public TimeSpan Period { get; }

    public decimal Open { get; }
    public decimal Close { get; }
    public decimal Average { get; }
    public decimal Max { get; }
    public decimal Min { get; }

    public PriceCandle(decimal[] values, DateTimeOffset dateTimeOffset, TimeSpan period) {
      if (values == null || values.Length == 0)
        throw new ArgumentException("The value array of an price candle cannot be null or empty.");
      Open = values.First();
      Close = values.Last();
      Average = values.Average();
      Max = values.Max();
      Min = values.Min();
      DateTimeOffset = dateTimeOffset;
      Period = period;
    }
  }
}
