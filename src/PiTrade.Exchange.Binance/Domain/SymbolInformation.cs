using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Binance.Domain {
  // example: https://api.binance.com/api/v3/exchangeInfo?symbol=GALAUSDT

  [JsonObject(MemberSerialization.OptIn)]
  internal class SymbolInformation {
    [JsonProperty(PropertyName = "symbol")]
    public string? MarketString { get; set; }

    [JsonProperty(PropertyName = "baseAsset")]
    public string? BaseAsset { get; set; }

    [JsonProperty(PropertyName = "quoteAsset")]
    public string? QuoteAsset { get; set; }

    [JsonProperty(PropertyName = "isSpotTradingAllowed")]
    public bool? IsSpotTradingAllowed { get; set; }

    [JsonProperty(PropertyName = "filters")]
    public IEnumerable<FilterInformation>? Filters { get; set; }

    [JsonIgnore]
    public int? BaseAssetPrecision =>
      CalcPrecision(Filters?.FirstOrDefault(x => x.FilterType == "LOT_SIZE")?.StepSize);


    [JsonIgnore]
    public int? QuoteAssetPrecision => 
      CalcPrecision(Filters?.FirstOrDefault(x => x.FilterType == "PRICE_FILTER")?.TickSize);


    private static int? CalcPrecision(string? input) {
      if (input == null) return default(int?);
      var decimalSeparator = NumberFormatInfo.CurrentInfo.CurrencyDecimalSeparator;
      var digits = input.Split(decimalSeparator).Last();
      var position = digits.IndexOf("1");
      return (position == -1) ? 0 : position + 1;
    }
  }
}
