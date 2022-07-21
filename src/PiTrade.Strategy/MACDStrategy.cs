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
    private readonly IIndicator posMomentumVerification;
    private readonly IIndicator slow;
    private readonly IIndicator fast;
    private readonly IIndicator signal;

    private decimal profit;
    private decimal curHoldingQty;
    private decimal lastMACDSignalDiff;
    private Action<ITrade> state;

    private decimal MACD => fast.Value - slow.Value;
    

    public MACDStrategy(IMarket market, decimal amountPerBuy, TimeSpan interval, uint maxTicksPosMomentumVerification, uint maxTicksSlow = 26, uint maxTicksFast = 12, uint maxTicksSignal = 7) {
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.posMomentumVerification = new ExponentialMovingAverage(interval, maxTicksPosMomentumVerification);
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

    private bool IsBuySignal(decimal curPrice) {
      var signalLine = signal.Value;
      var macdSignalDiff = MACD - signalLine;
      var res =
        MACD < 0 &&
        signalLine < 0 &&
        lastMACDSignalDiff < 0 &&
        macdSignalDiff >= 0 && 
        posMomentumVerification.Value < curPrice;
      lastMACDSignalDiff = macdSignalDiff;
      return res;
    }

    private bool IsSellSignal(decimal curPrice) {
      var signalLine = signal.Value;
      return
        MACD < signalLine ||
        curPrice < posMomentumVerification.Value;

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
      if(IsBuySignal(trade.Price)) {
        state = EmptyState;
        var buyPrice = fast.Value;
        var qty = amountPerBuy / buyPrice;
        Console.WriteLine($"Buy {qty} for ~{buyPrice}");
        market
          .Buy(qty)
          .OnExecuted(Bought)
          .OnError((o,e) => state = BuyState)
          .Submit();
      }
 
    }

    private void Bought(IOrder buyOrder) {
      profit -= buyOrder.ExecutedAmount;
      curHoldingQty += buyOrder.ExecutedQuantity;
      state = SellState;
    }

    private void SellState(ITrade trade) {
      if(IsSellSignal(trade.Price)) {
        state = EmptyState;
        var sellPrice = fast.Value;
        Console.WriteLine($"Sell {curHoldingQty} for ~{sellPrice}");
        market
          .Sell(curHoldingQty)
          .OnExecuted(Sold)
          .OnError((o, e) => state = SellState)
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
