using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PiTrade.Strategy.ConfigDTOs {
  [JsonObject(MemberSerialization.OptOut)]
  public class GridTradingStrategyConfig {
    public decimal MinQuotePerGrid { get; set; }
    public decimal ReinvestProfitRatio { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public uint GridCount { get; set; }
    public decimal SellThreshold { get; set; }
    public bool AutoDisable { get; set; }
  }
}
