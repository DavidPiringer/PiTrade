using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Strategy.Util {
  internal static class NumSpace {
    public static IEnumerable<decimal> Linear(decimal start, decimal end, uint count) {
      var s = Math.Max(start, end);
      var e = Math.Min(start, end);
      var stepSize = (s - e) / (count + 2);
      for (decimal i = (s - stepSize); i > (e + stepSize); i -= stepSize)
        yield return i;
    }
  }
}
