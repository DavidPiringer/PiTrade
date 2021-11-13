using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Extensions
{
  public static class DecimalExtensions
  {
    public static decimal RoundUp(this decimal d, int precision = 8)
    {
      var factor = (decimal)Math.Pow(10.0d, precision);
      return Math.Ceiling(d * factor) / factor;
    }

    public static decimal RoundDown(this decimal d, int precision = 8)
    {
      var factor = (decimal)Math.Pow(10.0d, precision);
      return Math.Floor(d * factor) / factor;
    }
  }
}
