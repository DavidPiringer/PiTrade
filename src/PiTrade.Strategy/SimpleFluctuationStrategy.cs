using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Indicators;

namespace PiTrade.Strategy {
  public class SimpleFluctuationStrategy {
    private readonly IMarket market;
    private readonly MovingAverageConvergenceDivergence macd;
    private readonly decimal amountPerBuy;
    private readonly decimal sellMultiplier;
    private readonly decimal emergencyMultiplier;

    private decimal profit;
    private Action<ITrade> state;

    public SimpleFluctuationStrategy(IMarket market, decimal amountPerBuy, decimal sellMultiplier, decimal emergencyMultiplier) {
      this.market = market;
      this.macd = new MovingAverageConvergenceDivergence(TimeSpan.FromMinutes(1));
      this.amountPerBuy = amountPerBuy;
      this.sellMultiplier = sellMultiplier;
      this.emergencyMultiplier = emergencyMultiplier;
      state = BuyState;
    }
    public void Start() {
      market.Subscribe(OnTrade);
    }

    public void Stop() {
      market.Unsubscribe(OnTrade);
    }

    private void OnTrade(ITrade trade) {
      macd.OnTrade(trade);
      state(trade);
    }

    private void EmptyState(ITrade trade) { }

    private void BuyState(ITrade trade) {
      if (!macd.IsUptrend)
        return;

      var buyPrice = trade.Price;
      var qty = amountPerBuy / buyPrice;
      Console.WriteLine($"Buy {qty} for {buyPrice}");
      state = EmptyState;
      market
        .Buy(qty)
        .For(buyPrice)
        .OnExecuted(Sell) //TODO: partial filled .. when cancelled
        .OnError(HandleError)
        .OnCancel(o => state = BuyState)
        .CancelAfter(TimeSpan.FromSeconds(15))
        .Submit();
    }

    private void Sell(IOrder buyOrder) {
      profit -= buyOrder.ExecutedAmount;
      var sellPrice = buyOrder.ExecutedPrice + sellMultiplier;
      var qty = buyOrder.ExecutedQuantity;
      Console.WriteLine($"Sell {qty} for {sellPrice}");
      market
        .Sell(qty)
        .For(sellPrice)
        .OnExecuted(Sold)
        .OnError(HandleError)
        .OnCancel(EmergencySell)
        .CancelIf(CheckEmergencySell)
        .Submit();
    }

    private void Sold(IOrder sellOrder) {
      profit += sellOrder.ExecutedAmount;
      Console.WriteLine($"Profit = {profit}");
      if(profit > -2m)
        state = BuyState;
      else
        Console.WriteLine("Shutdown, to much loss!");
    }

    private void EmergencySell(IOrder sellOrder) {
      Console.WriteLine("Emergency Sell");
      market
        .Sell(sellOrder.Quantity)
        .OnExecuted(Sold)
        .Submit();
    }

    private bool CheckEmergencySell(IOrder sellOrder, ITrade trade) => 
      trade.Price < (sellOrder.Price - emergencyMultiplier) || !macd.IsUptrend;

    private void HandleError(IOrder order, Exception ex) {
      Console.WriteLine($"An Error occured! -> {ex.Message}");
    }
  }
}
