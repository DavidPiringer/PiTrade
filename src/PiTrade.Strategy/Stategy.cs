using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Entities;

namespace PiTrade.Strategy {
  public abstract class Stategy {
    protected IMarket Market { get; }

    protected Stategy(IMarket market) {
      this.Market = market;
    }


    public virtual async Task Run() => await Market.Connect();

  }
}
