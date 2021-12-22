using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Entities {
  public class OrderFill {
    
    public decimal Quantity { get; }
    public decimal Price { get; }
    public decimal Amount => Quantity * Price;

    internal OrderFill(decimal quantity, decimal price) { 
      this.Quantity = quantity;
      this.Price = price;
    }
  }
}
