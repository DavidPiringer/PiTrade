using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Indicators;

namespace PiTrade.Strategy {
  public class StandardDeviationStrategy {
    private readonly object locker = new object();

    private readonly IMarket market;
    private readonly IIndicator indicator;
    private readonly decimal amountPerBuy;

    private bool inTrade = false;

    public StandardDeviationStrategy(IMarket market, decimal amountPerBuy, TimeSpan interval, uint maxTicks) { 
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.indicator = new ExponentialMovingAverage(market, interval, maxTicks);
    }

    public void Start() {
      market.SubscribeAsync(OnTrade);
    }

    public void Stop() {
      market.UnsubscribeAsync(OnTrade);
    }

    private async Task OnTrade(ITrade trade) {
      if (!indicator.IsReady)
        return;

      // To prevent race conditions
      bool canTrade = false;
      lock (locker) {
        canTrade = !inTrade;
        inTrade = true;
      }

      if(canTrade) {
        var buyPrice = indicator.Value - indicator.StandardDeviation;
        var sellPrice = indicator.Value + indicator.StandardDeviation;
        var qty = amountPerBuy / (indicator.Value - indicator.StandardDeviation);
        var test = qty * buyPrice;
        await market
          .Buy(qty)
          .For(buyPrice)
          .OnExecutedAsync(o => Sell(o, sellPrice))
          .OnError(_ => CleanUp())
          .Submit();
        Console.WriteLine($"Buy {qty} for {buyPrice}");
      }
    }

    private async Task Sell(IOrder buyOrder, decimal sellPrice) {
      var qty = buyOrder.ExecutedQuantity;
      if (qty * sellPrice < amountPerBuy)
        sellPrice *= 1.000001m;
      await market
        .Sell(qty)
        .For(sellPrice)
        .OnExecuted(o => CleanUp(buyOrder, o))
        .OnError(_ => CleanUp())
        .Submit();
      Console.WriteLine($"Sell {qty} for {sellPrice}");
    }

    private void CleanUp(IOrder? buyOrder = null, IOrder? sellOrder = null) {
      if (buyOrder != null && sellOrder != null) { 
        var profit = sellOrder.ExecutedAmount - buyOrder.ExecutedAmount;
        Console.WriteLine($"Profit = {profit}");
      } else {
        Console.WriteLine("An Error occured!");
      }
      lock (locker)
        inTrade = false;
    }


  }
}
