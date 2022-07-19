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
    decimal ValuePreview { get; }
    decimal Variance { get; }
    decimal StandardDeviation { get; }
    /// <summary>
    /// Call this method to update the indicator value.
    /// </summary>
    void OnTrade(ITrade trade);
    /// <summary>
    /// Call this method to update the indicator value.
    /// </summary>
    void OnTrade(decimal value);
  }
}
