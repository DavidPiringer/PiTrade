using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Strategy.Util {
  internal static class NumSpace {
    public static IEnumerable<decimal> Linear(decimal start, decimal end, uint count, bool cutFirst = false) {
      var s = Math.Max(start, end);
      var e = Math.Min(start, end);
      var stepSize = (s - e) / count;
      if(cutFirst)
        s -= stepSize;

      for (int i = 0; i < count; ++i)
        yield return s - i * stepSize;
    }
  }
}
