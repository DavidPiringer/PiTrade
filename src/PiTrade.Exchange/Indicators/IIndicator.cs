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
    decimal Trend { get; }
    double Slope { get; }
    bool IsBearish { get}
    bool IsBullish { get; }
    void Update(params PriceCandle[] candles);
  }
}
