﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange.Entities;

namespace PiTrade.Exchange
{
  public interface IExchange
  {
    IEnumerable<Order> ActiveOrders { get; }
    IEnumerable<Market> AvailableMarkets { get; }
    Task<Order> Get(int id);
    Task<Order> Buy(Market market, decimal price, decimal quantity);
    Task<Order> Sell(Market market, decimal price, decimal quantity);
    Task Cancel(Order order);
    Task CancelAll(Market market);
    IExchangeFeed GetFeed(Market market);
    // TODO: get wallet
  }
}
