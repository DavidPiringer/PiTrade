using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Indicators {
  public interface IIndicator {
    TimeSpan Period { get; }
    bool IsReady { get; }
    decimal Value { get; }
    double Slope { get; }
    Task Update(params PriceCandle[] candles);
    void Listen(Func<IIndicator, Task> fnc);
  }
}
