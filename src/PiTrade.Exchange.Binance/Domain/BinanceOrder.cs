using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class BinanceOrder {
    [JsonProperty(PropertyName = "orderId")]
    public int Id { get; set; } = -1;
  }
}
