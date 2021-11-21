﻿using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange {
  public interface IMarket {
    decimal CurrentPrice { get; }
    IExchange Exchange { get; }
    Symbol Asset { get; }
    Symbol Quote { get; }
    int AssetPrecision { get; }
    int QuotePrecision { get; }
    IEnumerable<Order> ActiveOrders { get; }
    IEnumerable<IIndicator> Indicators { get; }

    void AddIndicator(IIndicator indicator);


    void Register(IMarketListener listener);
    void Unregister(IMarketListener listener);
  }
}
