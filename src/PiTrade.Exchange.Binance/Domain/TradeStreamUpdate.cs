using Newtonsoft.Json;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class TradeStreamUpdate : ITradeUpdate {
    [JsonProperty(PropertyName = "s")]
    public string? Symbol { get; set; }

    [JsonProperty(PropertyName = "p")]
    public decimal Price { get; set; }

    [JsonProperty(PropertyName = "q")]
    public decimal Quantity { get; set; }

    [JsonProperty(PropertyName = "b")]
    public long OIDBuyer { get; set; }

    [JsonProperty(PropertyName = "a")]
    public long OIDSeller { get; set; }

    [JsonIgnore]
    public Symbol Asset { get; set; } = new Symbol("None");

    [JsonIgnore]
    public Symbol Quote { get; set; } = new Symbol("None");

    public bool Match(long id) => id == OIDSeller || id == OIDBuyer;
  }
}
