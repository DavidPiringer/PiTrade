using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange {
  public interface ITrade {
    decimal Price { get; set; }
    decimal Quantity { get; set; }
    decimal Commission { get; set; }
  }
}
