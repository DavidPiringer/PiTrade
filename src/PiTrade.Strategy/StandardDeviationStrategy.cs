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
    private readonly decimal stdDevFactor;

    private bool inTrade = false;
    private decimal profit;

    public StandardDeviationStrategy(IMarket market, decimal amountPerBuy, decimal stdDevFactor, TimeSpan interval, uint maxTicks) { 
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.stdDevFactor = stdDevFactor;
      this.indicator = new ExponentialMovingAverage(market, interval, maxTicks);
    }

    public void Start() {
      market.Subscribe(OnTrade);
    }

    public void Stop() {
      market.Unsubscribe(OnTrade);
    }

    private void OnTrade(ITrade trade) {
      if (!indicator.IsReady)
        return;

      var stddev = indicator.StandardDeviation * stdDevFactor;
      var curPrice = indicator.Value;

      if(inTrade) {
        inTrade = true;
        var buyPrice = indicator.Value - stddev;
        var sellPrice = indicator.Value + stddev;
        var qty = amountPerBuy / (indicator.Value - stddev);
        var test = qty * buyPrice;
        market
          .Buy(qty)
          .For(buyPrice)
          .OnExecuted(o => Sell(o, sellPrice, stddev))
          .OnCancel(_ => CleanUp())
          .OnError(HandleError)
          .CancelAfter(TimeSpan.FromSeconds(6))
          .Submit();
        Console.WriteLine($"Buy {qty} for {buyPrice}");
      }
    }

    private void Sell(IOrder buyOrder, decimal sellPrice, decimal stddev) {
      var qty = buyOrder.ExecutedQuantity;
      // increase the sellPrice minimal to match "amountPerBuy"
      if (qty * sellPrice < amountPerBuy)
        sellPrice *= 1.000001m;
      market
        .Sell(qty)
        .For(sellPrice)
        .OnExecuted(o => CleanUp(buyOrder, o))
        .OnError(HandleError)
        .OnCancel(o => EmergencySell(o, buyOrder))
        .CancelIf((o,t) => CheckEmergencySell(o,t,stddev))
        .Submit();
      Console.WriteLine($"Sell {qty} for {sellPrice}");
    }

    private void CleanUp(IOrder? buyOrder = null, IOrder? sellOrder = null) {
      if (buyOrder != null && sellOrder != null) {
        profit += sellOrder.ExecutedAmount - buyOrder.ExecutedAmount;
        Console.WriteLine($"Profit = {profit}");
      }
      inTrade = false;
    }

    private bool CheckEmergencySell(IOrder order, ITrade trade, decimal stddev) {
      var thresholdPrice = order.Price - stdDevFactor * 4m * stddev;
      return trade.Price < thresholdPrice;
    }

    private void EmergencySell(IOrder order, IOrder buyOrder) {
      Console.WriteLine("!!! Emergency Sell !!!");
      market
        .Sell(order.Quantity)
        .OnExecuted(async o => {
          Task.Delay(TimeSpan.FromSeconds(10)).Wait();
          CleanUp(buyOrder, o);
        }).Submit();
    }

    private void HandleError(IOrder order, Exception ex) {
      Console.WriteLine($"An Error occured! -> {ex.Message}");
    }


  }
}
