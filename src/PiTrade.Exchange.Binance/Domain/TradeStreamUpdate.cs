using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain
{
  [JsonObject(MemberSerialization.OptIn)]
  internal class TradeStreamUpdate
  {
    [JsonProperty(PropertyName = "p")]
    public decimal Price { get; set; }

    [JsonProperty(PropertyName = "q")]
    public decimal Quantity { get; set; }

    [JsonProperty(PropertyName = "b")]
    public int OIDBuyer { get; set; }

    [JsonProperty(PropertyName = "a")]
    public int OIDSeller { get; set; }
  }
}
