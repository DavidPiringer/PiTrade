using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class Symbol {
    // TODO: add static editable symbol cache
    public static Symbol BTC => new Symbol("BTC");
    public static Symbol ETH => new Symbol("ETH");
    public static Symbol SHIB => new Symbol("SHIB");
    public static Symbol BNB => new Symbol("BNB");
    public static Symbol WIN => new Symbol("WIN");
    public static Symbol SXP => new Symbol("SXP");
    public static Symbol ADA => new Symbol("ADA");
    public static Symbol LUNA => new Symbol("LUNA");
    public static Symbol PORTO => new Symbol("PORTO");
    public static Symbol REN => new Symbol("REN");
    public static Symbol FRONT => new Symbol("FRONT");
    public static Symbol FTM => new Symbol("FTM");
    public static Symbol ATOM => new Symbol("ATOM");
    public static Symbol DOT => new Symbol("DOT");
    public static Symbol GLMR => new Symbol("GLMR");
    public static Symbol BADGER => new Symbol("BADGER");
    public static Symbol TFUEL => new Symbol("TFUEL");
    public static Symbol LOKA => new Symbol("LOKA");
    public static Symbol GALA => new Symbol("GALA");
    public static Symbol DOGE => new Symbol("DOGE");
    public static Symbol MANA => new Symbol("MANA");
    public static Symbol SAND => new Symbol("SAND");
    public static Symbol SOL => new Symbol("SOL");
    public static Symbol GTO => new Symbol("GTO");
    public static Symbol DREP => new Symbol("DREP");
    public static Symbol KEY => new Symbol("KEY");
    public static Symbol DAR => new Symbol("DAR");
    public static Symbol MITH => new Symbol("MITH");
    public static Symbol COCOS => new Symbol("COCOS");
    public static Symbol STRAX => new Symbol("STRAX");
    public static Symbol ROSE => new Symbol("ROSE");
    public static Symbol MBOX => new Symbol("MBOX");
    public static Symbol EUR => new Symbol("EUR");
    public static Symbol USDT => new Symbol("USDT");

    private string Value { get; set; }

    public Symbol(string symbol) {
      Value = symbol;
    }

    public override string ToString() => Value.ToUpper();
    public override int GetHashCode() => Value.GetHashCode();
    public override bool Equals(object? obj) => obj is Symbol other && Value == other.Value;

    public static bool operator ==(Symbol lhs, Symbol rhs) => lhs.Equals(rhs);
    public static bool operator !=(Symbol lhs, Symbol rhs) => !(lhs == rhs);
  }
}
