using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities
{
  public class OrderSide
  {
    public static OrderSide BUY => new OrderSide("BUY");
    public static OrderSide SELL => new OrderSide("SELL");

    private string Value { get; set; }
    public OrderSide(string value)
    {
      Value = value;
    }

    public override string ToString() => Value;
  }
}
