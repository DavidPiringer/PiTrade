using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Domain
{
  public class OrderType
  {
    public static OrderType BUY => new OrderType("BUY");
    public static OrderType SELL => new OrderType("SELL");

    private string Value { get; set; }
    public OrderType(string value)
    {
      Value = value;
    }

    public override string ToString() => Value;
  }
}
