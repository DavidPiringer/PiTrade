using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class ExchangeInformation {
    [JsonProperty(PropertyName = "serverTime")]
    public long ServerTime { get; set; }

    [JsonProperty(PropertyName = "symbols")]
    public IEnumerable<SymbolInformation>? Symbols { get; set; }
  }
}
