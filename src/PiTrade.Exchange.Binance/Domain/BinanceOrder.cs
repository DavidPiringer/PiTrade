using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class BinanceOrder {
    [JsonProperty(PropertyName = "symbol")]
    public string? Symbol { get; set; }

    [JsonProperty(PropertyName = "orderId")]
    public long Id { get; set; } = -1;

    [JsonProperty(PropertyName = "status")]
    public string? Status { get; set; }

    [JsonProperty(PropertyName = "fills")]
    public IEnumerable<BinanceOrderFill>? Fills { get; set; }

  }
}
