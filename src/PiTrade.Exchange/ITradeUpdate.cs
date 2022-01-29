using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public interface ITradeUpdate {
    Symbol Asset { get; set; }
    Symbol Quote { get; set; }
    decimal Price { get; set; }
    decimal Quantity { get; set; }
    bool Match(long orderId);
  }
}
