using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities
{
  public class Symbol
  {
    // TODO: add static editable symbol cache
    public static Symbol BTC => new Symbol("BTC");
    public static Symbol ETC => new Symbol("ETC");
    public static Symbol BNB => new Symbol("BNB");
    public static Symbol EUR => new Symbol("EUR");

    private string Value { get; set; }

    public Symbol(string symbol)
    {
      Value = symbol;
    }

    public override string ToString() => Value.ToUpper();
    public override int GetHashCode() => Value.GetHashCode();
  }
}
