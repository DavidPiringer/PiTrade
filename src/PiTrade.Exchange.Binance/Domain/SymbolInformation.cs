using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain
{
  // example: https://api.binance.com/api/v3/exchangeInfo?symbol=GALAUSDT

  [JsonObject(MemberSerialization.OptIn)]
  internal class SymbolInformation
  {
    [JsonProperty(PropertyName = "symbol")]
    public string? MarketString { get; set; }

    [JsonProperty(PropertyName = "baseAsset")]
    public string? BaseAsset { get; set; }

    [JsonProperty(PropertyName = "quoteAsset")]
    public string? QuoteAsset { get; set; }

    [JsonProperty(PropertyName = "filters")]
    public IEnumerable<FilterInformation>? Filters { get; set; }
  }
}
