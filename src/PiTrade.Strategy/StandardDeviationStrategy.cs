using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Indicators;

namespace PiTrade.Strategy {
  public class StandardDeviationStrategy {
    private readonly IMarket market;
    private readonly IIndicator indicator;
    private readonly decimal amountPerBuy;
    private readonly decimal stdDevBuyMultiplier;
    private readonly decimal stdDevSellMultiplier;

    private decimal profit;
    private Action<ITrade> state;
    private decimal curHoldingQty;

    public StandardDeviationStrategy(IMarket market, decimal amountPerBuy, decimal stdDevBuyMultiplier, decimal stdDevSellMultiplier, TimeSpan interval, uint maxTicks) { 
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.stdDevBuyMultiplier = stdDevBuyMultiplier;
      this.stdDevSellMultiplier = stdDevSellMultiplier;
      this.indicator = new SimpleMovingAverage(market, interval, maxTicks, IndicatorValueType.Typical);
      state = BuyState;
    }

    public void Start() {
      market.Subscribe(OnTrade);
    }

    public void Stop() {
      market.Unsubscribe(OnTrade);
    }

    private void OnTrade(ITrade trade) {
      if (indicator.IsReady)
        state(trade);
      else
        Console.WriteLine("Indicator not ready!");
    }

    private void EmptyState(ITrade trade) { }

    private void BuyState(ITrade trade) {
      var buyPrice = indicator.Value - (indicator.StandardDeviation * stdDevBuyMultiplier);
      var qty = amountPerBuy / buyPrice;
      Console.WriteLine($"Buy {qty} for {buyPrice}");
      state = EmptyState;
      market
        .Buy(qty)
        .For(buyPrice)
        .OnExecuted(Bought)
        .OnError(HandleError)
        .OnCancel(o => state = BuyState)
        .CancelAfter(indicator.Period)
        .Submit();
    }

    private void SellState(ITrade trade) {
      var sellPrice = indicator.Value + (indicator.StandardDeviation * stdDevSellMultiplier);
      Console.WriteLine($"Sell {curHoldingQty} for {sellPrice}");
      state = EmptyState;
      market
        .Sell(curHoldingQty)
        .For(sellPrice)
        .OnExecuted(Sold)
        .OnError(HandleError)
        .OnCancel(o => state = SellState)
        .CancelAfter(indicator.Period)
        .Submit();
    }

    private void Bought(IOrder buyOrder) {
      curHoldingQty += buyOrder.ExecutedQuantity;
      profit -= buyOrder.ExecutedAmount;
      state = SellState;
    }

    private void Sold(IOrder sellOrder) {
      curHoldingQty -= sellOrder.ExecutedQuantity;
      profit += sellOrder.ExecutedAmount;
      Console.WriteLine($"Profit = {profit}");
      state = BuyState;
    }

    private void HandleError(IOrder order, Exception ex) {
      Console.WriteLine($"An Error occured! -> {ex.Message}");
    }

  }
}
