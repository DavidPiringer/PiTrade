using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange {
  public interface IOrderListener {
    Task OnPriceUpdate(decimal price);
    Task OnBuy(Order order);
    Task OnSell(Order order);
  }
}
