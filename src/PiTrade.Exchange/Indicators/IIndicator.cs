using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Indicators {
  public interface IIndicator {
    uint MaxTicks { get; }
    TimeSpan Period { get; }
    bool IsReady { get; }
    decimal Value { get; }
    //void Update(params PriceCandle[] candles);
  }
}
