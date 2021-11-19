using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Indicators {
  public interface IIndicator {
    public IEnumerable<decimal> Values { get; }
    decimal Update(decimal value);
  }
}
