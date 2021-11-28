using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public interface ITradeUpdate {
    decimal Price { get; set; }
    decimal Quantity { get; set; }
    bool Match(Order order);
  }
}
