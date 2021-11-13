using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities
{
  public class Market
  {
    public Symbol Asset { get; private set; }
    public Symbol Quote { get; private set; }

    public int AssetPrecision { get; private set; }
    public int QuotePrecision { get; private set; }

    public Market(Symbol asset, Symbol quote, int assetPrecision, int quotePrecision)
    {
      Asset = asset;
      Quote = quote;
      AssetPrecision = assetPrecision;
      QuotePrecision = quotePrecision;
    }

    public override string ToString() => $"{Asset}{Quote}";
  }
}
