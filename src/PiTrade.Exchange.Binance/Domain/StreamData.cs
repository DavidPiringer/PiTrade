using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class StreamData {
    [JsonProperty(PropertyName = "data")]
    public TradeStreamUpdate? Update { get; set; }
  }
}
