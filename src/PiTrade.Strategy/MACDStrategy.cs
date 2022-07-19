using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Indicators;

namespace PiTrade.Strategy {
  public class MACDStrategy {
    private readonly IMarket market;
    private readonly decimal amountPerBuy;
    private readonly IIndicator slow;
    private readonly IIndicator fast;
    private readonly IIndicator signal;

    private decimal profit;
    private decimal curHoldingQty;
    private Action<ITrade> state;

    private decimal MACD => fast.Value - slow.Value;
    private bool IsBullish => MACD > signal.Value;

    public MACDStrategy(IMarket market, decimal amountPerBuy, TimeSpan interval, uint maxTicksSlow = 26, uint maxTicksFast = 12, uint maxTicksSignal = 7) {
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.slow = new ExponentialMovingAverage(interval, maxTicksSlow);
      this.fast = new ExponentialMovingAverage(interval, maxTicksFast);
      this.signal = new SimpleMovingAverage(interval, maxTicksSignal);
      state = BuyState;
    }

    public void Start() {
      market.Subscribe(OnTrade);
    }

    public void Stop() {
      market.Unsubscribe(OnTrade);
    }

    private void OnTrade(ITrade trade) {
      slow.OnTrade(trade);
      fast.OnTrade(trade);
      signal.OnTrade(MACD);
      if (slow.IsReady && fast.IsReady && signal.IsReady)
        state(trade);
    }

    private void EmptyState(ITrade trade) { }

    private void BuyState(ITrade trade) {
      if(IsBullish) {
        state = EmptyState;
        var buyPrice = fast.Value;
        var qty = amountPerBuy / buyPrice;
        Console.WriteLine($"Buy {qty} for ~{buyPrice}");
        market
          .Buy(qty)
          .OnExecuted(Bought)
          .Submit();
      }
 
    }

    private void Bought(IOrder buyOrder) {
      profit -= buyOrder.ExecutedAmount;
      curHoldingQty += buyOrder.ExecutedQuantity;
      state = SellState;
    }

    private void SellState(ITrade trade) {
      if(!IsBullish) {
        state = EmptyState;
        var sellPrice = fast.Value;
        Console.WriteLine($"Sell {curHoldingQty} for ~{sellPrice}");
        market
          .Sell(curHoldingQty)
          .OnExecuted(Sold)
          .Submit();
      }
    }

    private void Sold(IOrder sellOrder) {
      profit += sellOrder.ExecutedAmount;
      curHoldingQty -= sellOrder.ExecutedQuantity;
      Console.WriteLine($"Profit = {profit}");
      state = BuyState;
    }
  }
}
