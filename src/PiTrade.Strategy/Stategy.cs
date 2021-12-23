using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Entities;

namespace PiTrade.Strategy {
  public abstract class Stategy : IOrderListener {
    protected IMarket Market { get; }
    protected IMarketHandle? Handle { get; private set; }

    protected Stategy(IMarket market) {
      this.Market = market;
    }


    public async Task Run() {
      Handle = Market.GetMarketHandle(out Task awaitTask, this);
      await awaitTask;
    }

    public abstract Task OnBuy(Order order);
    public abstract Task OnPriceUpdate(decimal price);
    public abstract Task OnSell(Order order);
  }
}
