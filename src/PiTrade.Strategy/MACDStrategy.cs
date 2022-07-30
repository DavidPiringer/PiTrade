﻿using System;
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
    private readonly IIndicator posMomentumVerification;
    private readonly MovingAverageConvergenceDivergence macd;

    private decimal profit;
    private decimal curHoldingQty;
    private decimal lastMACDSignalDiff;
    private Action<ITrade> state;

    

    public MACDStrategy(IMarket market, decimal amountPerBuy, TimeSpan interval, uint maxTicksPosMomentumVerification = 200, uint maxTicksSlow = 30, uint maxTicksFast = 14, uint maxTicksSignal = 14) {
      this.market = market;
      this.amountPerBuy = amountPerBuy;
      this.posMomentumVerification = new ExponentialMovingAverage(interval, maxTicksPosMomentumVerification);
      this.macd = new MovingAverageConvergenceDivergence(interval, maxTicksSlow, maxTicksFast, maxTicksSignal);
      state = BuyState;
    }

    public void Start() {
      Log.Info($"[MACDStrategy] [{market.QuoteAsset}{market.BaseAsset}] START");
      state = BuyState;
      market.Subscribe(OnTrade);
    }

    public void Stop() {
      Log.Info($"[MACDStrategy] [{market.QuoteAsset}{market.BaseAsset}] STOP");
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
      macd.OnTrade(trade);
      posMomentumVerification.OnTrade(trade);
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
        var qty = amountPerBuy / buyPrice;
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
      Log.Info($"[{market.QuoteAsset}{market.BaseAsset}] PROFIT = {profit}");
      if (profit > -1m)
        state = BuyState;
      else {
        Log.Warn($"[{market.QuoteAsset}{market.BaseAsset}] SHUTDOWN - LOSS TOO HIGH");
        Stop();
      }
    }
  }
}