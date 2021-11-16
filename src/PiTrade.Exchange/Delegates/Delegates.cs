using PiTrade.Exchange.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange.Delegates
{
  public delegate Task OrderEvent(Order order);
  public delegate Task PriceUpdateEvent(decimal price);
}
