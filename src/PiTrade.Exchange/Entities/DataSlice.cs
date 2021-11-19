using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class DataSlice {
    public decimal[] Values { get; }
    public TimeSpan Period { get; }
    public DataSlice(decimal[] values, TimeSpan period) {
      Values = values;
      Period = period;
    }
  }
}
