using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain {
  [JsonObject(MemberSerialization.OptIn)]
  internal class FilterInformation {
    [JsonProperty(PropertyName = "filterType")]
    public string? FilterType { get; set; }

    // PRICE_FILTER
    [JsonProperty(PropertyName = "minPrice")]
    public string? MinPrice { get; set; }

    [JsonProperty(PropertyName = "maxPrice")]
    public string? MaxPrice { get; set; }

    [JsonProperty(PropertyName = "tickSize")]
    public string? TickSize { get; set; }

    // LOT_SIZE
    [JsonProperty(PropertyName = "minQty")]
    public string? MinQuantity { get; set; }

    [JsonProperty(PropertyName = "maxQty")]
    public string? MaxQuantity { get; set; }

    [JsonProperty(PropertyName = "stepSize")]
    public string? StepSize { get; set; }
  }
}
