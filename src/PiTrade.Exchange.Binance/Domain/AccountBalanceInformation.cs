using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class AccountBalanceInformation {
    [JsonProperty(PropertyName = "asset")]
    public string? Asset { get; set; }

    [JsonProperty(PropertyName = "free")]
    public decimal Free { get; set; }

  }
}
