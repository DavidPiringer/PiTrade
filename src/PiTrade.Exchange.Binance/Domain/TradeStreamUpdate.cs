using Newtonsoft.Json;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class TradeStreamUpdate : ITradeUpdate {
    [JsonProperty(PropertyName = "p")]
    public decimal Price { get; set; }

    [JsonProperty(PropertyName = "q")]
    public decimal Quantity { get; set; }

    [JsonProperty(PropertyName = "b")]
    public long OIDBuyer { get; set; }

    [JsonProperty(PropertyName = "a")]
    public long OIDSeller { get; set; }

    public bool Match(long id) => id == OIDSeller || id == OIDBuyer;
  }
}
