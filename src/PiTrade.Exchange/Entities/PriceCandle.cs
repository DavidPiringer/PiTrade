using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class PriceCandle {
    public decimal[] Values { get; }
    public TimeSpan Period { get; }

    public decimal Open => Values.First();
    public decimal Close => Values.Last();
    public decimal Average => Values.Average();
    public decimal Max => Values.Max();
    public decimal Min => Values.Min(); 

    public PriceCandle(decimal[] values, TimeSpan period) {
      if (values == null || values.Length == 0)
        throw new ArgumentException("The value array of an price candle cannot be null or empty.");
      Values = values;
      Period = period;
    }
  }
}
