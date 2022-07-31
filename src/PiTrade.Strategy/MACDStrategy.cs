using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;

namespace PiTrade.Strategy {
  public class MACDStrategy {
    private readonly IMarket market;
    private readonly decimal amountPerBuy;
    private readonly decimal maxLoss;
    private readonly bool includeProfitToAmountPerBuy;
    private readonly IIndicator posMomentumVerification;
    private readonly MovingAverageConvergenceDivergence macd;
    private readonly uint maxTicks;

    private decimal profit;
    private decimal curAmountPerBuy;
    private decimal curHoldingQty;
    private decimal lastMACDSignalDiff;
    private Action<ITrade> state;

    

    public MACDStrategy(IMarket market, decimal amountPerBuy, decimal maxLoss, bool includeProfitToAmountPerBuy, TimeSpan interval, uint maxTicksPosMomentumVerification = 200, uint maxTicksSlow = 30, uint maxTicksFast = 14, uint maxTicksSignal = 14) {
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.curAmountPerBuy = amountPerBuy;
      this.maxLoss = -1m * Math.Abs(maxLoss); //make sure maxLoss is negative
      this.includeProfitToAmountPerBuy = includeProfitToAmountPerBuy;
      this.posMomentumVerification = new ExponentialMovingAverage(interval, maxTicksPosMomentumVerification);
      this.macd = new MovingAverageConvergenceDivergence(interval, maxTicksSlow, maxTicksFast, maxTicksSignal);
      this.maxTicks = (new[] { maxTicksPosMomentumVerification, maxTicksFast, maxTicksSlow, maxTicksSignal }).Max();
      state = BuyState;
    }

    public override string ToString() => $"AmountPerBuy = {amountPerBuy}, MaximalLoss = {maxLoss}, IncludeProfitToAmountPerBuy = {includeProfitToAmountPerBuy}";

    public void Start() {
      Log.Info($"[MACDStrategy] [{market.QuoteAsset}{market.BaseAsset}] START [{ToString()}]");
      var task = market.GetMarketData(Exchange.Enums.PriceCandleInterval.Minute1, maxTicks);
      task.Wait();
      foreach(var candle in task.Result) {
        macd.Add(candle);
        posMomentumVerification.Add(candle);
      }

      state = BuyState;
      market.Subscribe(OnTrade);
    }

    public void Stop() {
      Log.Info($"[MACDStrategy] [{market.QuoteAsset}{market.BaseAsset}] STOP [{ToString()}]");
      state = EmptyState;
      market.Unsubscribe(OnTrade);
    }

    private bool IsBuySignal(decimal curPrice) {
      var macdVal = macd.Value;
      var signalVal = macd.Signal;
      var macdSignalDiff = macdVal - signalVal;
      var res =
        macdVal < 0 &&
        signalVal < 0 &&
        lastMACDSignalDiff < 0 &&
        macdSignalDiff >= 0 &&
        curPrice < posMomentumVerification.Value;
      lastMACDSignalDiff = macdSignalDiff;
      return res;
    }

    private bool IsSellSignal(decimal curPrice) {
      return !macd.IsUptrend;
    }

    private void OnTrade(ITrade trade) {
      macd.Add(trade);
      posMomentumVerification.Add(trade);
      if (macd.IsReady && posMomentumVerification.IsReady)
        state(trade);
      else {
        Log.Info($"[MACDStrategy] [{market.QuoteAsset}{market.BaseAsset}] WARMING UP ...");
      }
    }

    private void EmptyState(ITrade trade) { }

    private void BuyState(ITrade trade) {
      if(IsBuySignal(trade.Price)) {
        state = EmptyState;
        var buyPrice = macd.FastValue;
        var qty = curAmountPerBuy / buyPrice;
        Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] BUY {qty} @ MARKET");
        market
          .Buy(qty)
          .OnExecuted(Bought)
          .OnError((o,e) => {
            state = BuyState;
            Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] ERROR @ BUY");
          })
          .Submit();
      }
 
    }

    private void Bought(IOrder buyOrder) {
      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] BOUGHT {buyOrder}");
      profit -= buyOrder.ExecutedAmount;
      curHoldingQty += buyOrder.ExecutedQuantity;
      state = SellState;
    }

    private void SellState(ITrade trade) {
      if(IsSellSignal(trade.Price)) {
        state = EmptyState;
        var sellPrice = macd.FastValue;
        Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] SELL {curHoldingQty} @ MARKET");
        market
          .Sell(curHoldingQty)
          .OnExecuted(Sold)
          .OnError((o, e) => {
            state = SellState;
            Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] ERROR @ SELL");
          })
          .Submit();
      }
    }

    private void Sold(IOrder sellOrder) {
      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] SOLD {sellOrder}");
      profit += sellOrder.ExecutedAmount;
      curHoldingQty -= sellOrder.ExecutedQuantity;
      if (includeProfitToAmountPerBuy)
        curAmountPerBuy = amountPerBuy + profit;
      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] PROFIT = {profit}, CurAmountPerBuy = {curAmountPerBuy}");
      if (profit > maxLoss)
        state = BuyState;
      else {
        Log.Warn($"[{market.QuoteAsset}{market.BaseAsset}] SHUTDOWN - LOSS TOO HIGH");
        Stop();
      }
    }
  }
}
