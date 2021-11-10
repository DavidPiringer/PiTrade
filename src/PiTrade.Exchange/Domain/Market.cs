using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Domain
{
  public class Market
  {
    public Symbol Asset { get; private set; }
    public Symbol Quote { get; private set; }

    public Market(Symbol asset, Symbol quote)
    {
      Asset = asset;
      Quote = quote;
    }

    public override string ToString() => $"{Asset}{Quote}";
  }
}
