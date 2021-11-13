using PiTrade.Exchange.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange
{
  public delegate Task OrderEvent(Order order);
  public delegate Task PriceUpdateEvent(decimal price);

  public interface IExchangeFeed : IDisposable
  {
    event OrderEvent? OnBuy;
    event OrderEvent? OnSell;
    event PriceUpdateEvent? OnPriceUpdate;

    Task Run(CancellationToken token);
  }
}
