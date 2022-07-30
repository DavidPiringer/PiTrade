using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class BinanceSpotTrade : ITrade {
    [JsonProperty(PropertyName = "e")]
    public string? MessageType { get; set; }

    [JsonProperty(PropertyName = "E")]
    public long UnixEpoch { get; set; }

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
    public decimal Commission { get; set; }

  }
}
