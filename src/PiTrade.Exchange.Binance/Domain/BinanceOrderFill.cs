using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class BinanceOrderFill {
    [JsonProperty(PropertyName = "price")]
    public decimal Price { get; set; }

    [JsonProperty(PropertyName = "qty")]
    public decimal Quantity { get; set; }

    public BinanceSpotTrade ToSpotTrade() => new BinanceSpotTrade() {
      Price = Price,
      Quantity = Quantity
    };
  }
}
