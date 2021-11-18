﻿using System;
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
    public static Symbol PORTO => new Symbol("PORTO");
    public static Symbol GALA => new Symbol("GALA");
    public static Symbol DOGE => new Symbol("DOGE");
    public static Symbol MANA => new Symbol("MANA");
    public static Symbol SAND => new Symbol("SAND");
    public static Symbol STRAX => new Symbol("STRAX");
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
