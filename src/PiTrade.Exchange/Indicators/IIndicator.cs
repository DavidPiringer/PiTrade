using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Indicators {
  public interface IIndicator {
    TimeSpan Period { get; }
    decimal Value { get; }
    double Slope { get; }
    Task Update(PriceCandle value);
    void Listen(Func<IIndicator, Task> fnc);
  }
}
