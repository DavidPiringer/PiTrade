using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.DTOs {
  public struct MarketDTO {
    public Symbol Asset { get; set; }
    public Symbol Quote { get; set; }
    public int AssetPrecision { get; set; }
    public int QuotePrecision { get; set; }
  }
}
