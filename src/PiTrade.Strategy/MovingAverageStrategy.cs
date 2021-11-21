using PiTrade.Exchange;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Exchange.Extensions;
using PiTrade.Logging;
using PiTrade.Strategy.Domain;
using PiTrade.Strategy.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Strategy {
  public class MovingAverageStrategy {
    #region Properties
    private readonly IMarket Market;
    private readonly decimal CommissionFee = 0.00075m;
    private readonly decimal AvgMultiplier = 1.0025m;
    private readonly int RestartDelay = 5;
    private readonly object locker = new object();

    private decimal MaxQuote { get; set; }
    public decimal BuyStepSize { get; }
    public int MaxOrderCount => (int)Math.Floor(MaxQuote / BuyStepSize);
    public decimal OrdersUntilBelowBasePrice { get; }


    private bool IsTrading { get; set; } = false;
    private bool AlreadyClearing { get; set; } = false;
    private long TradeStart { get; set; }
    private long RestartTime { get; set; }
    private Order? SellOrder { get; set; } = null;
    private Order? CurBuyOrder { get; set; } = null;
    private ConcurrentQueue<BuyStep> BuySteps { get; } = new ConcurrentQueue<BuyStep>();
    private decimal Revenue { get; set; } = 0m;
    private decimal Profit => Revenue - Commission;
    private decimal Quantity { get; set; } = 0m;
    private decimal Commission { get; set; } = 0m;
    private decimal CurrentAmount { get; set; } = 0m;
    private IList<Order> ExecutedOrders { get; } = new List<Order>();
    #endregion

    #region Constructors
    public MovingAverageStrategy(IMarket market, decimal maxQuote, decimal buyStepSize, decimal ordersUntilBelowBasePrice) {
      Market = market;
      MaxQuote = maxQuote;
      BuyStepSize = buyStepSize;
      OrdersUntilBelowBasePrice = ordersUntilBelowBasePrice;
    }
    #endregion

    public async Task Run(CancellationToken token) {
      RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      await Market.Listen(OnBuy, OnSell, OnPriceUpdate, token);
      foreach (var order in Market.ActiveOrders.Where(x => x.Side == OrderSide.BUY && !x.IsFilled)) {
        await Market.Cancel(order);
      }
    }

    private async Task OnPriceUpdate(decimal price) {
      if (!IsTrading && (RestartTime + RestartDelay) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
        // TODO: nur kleine intervalle bis stop -> bei stop sofort verkaufen?
        await SetupTrade(price);
      } else if (IsTrading
          && SellOrder == null
          && !Market.ActiveOrders.Any(x => x.ExecutedQuantity > 0)
          && (TradeStart + 10) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
          && CurBuyOrder != null
          && CurBuyOrder.Price <= (price * 0.9975m)) {
        await Clear();
      }
    }

    private async Task OnBuy(Order order) {
      var lastFill = order.Fills.LastOrDefault();
      lock(locker) {
        Quantity += lastFill; /* TODO: add later if BNB is depleted - lastFill * CommissionFee;*/
        Quantity = Quantity.RoundDown(Market.AssetPrecision);
        Commission += lastFill * order.Price * CommissionFee;
        CurrentAmount += lastFill * order.Price;
        Revenue -= lastFill * order.Price;
      }
      

      // calc sellPrice and update sell order
      var avg = ExecutedOrders.Sum(x => AvgOrderWeight(x));
      avg += AvgOrderWeight(order);
      var sellPrice = avg * AvgMultiplier;
      await UpdateSellOrder(sellPrice);

      if (order.IsFilled) {
        ExecutedOrders.Add(order);
        await NextBuyStep();
      }
    }

    private async Task OnSell(Order order) {
      var lastFill = order.Fills.LastOrDefault();
      lock (locker) {
        Quantity -= lastFill;
        Quantity = Quantity.RoundDown(Market.AssetPrecision);
        Commission += lastFill * order.Price * CommissionFee;
        Revenue += lastFill * order.Price;
        if (AlreadyClearing) return;
      }

      if (order.IsFilled) {
        await Clear();
        PrintStats();
      }
    }

    private async Task SetupTrade(decimal basePrice) {
      IsTrading = true;
      TradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      Quantity = (await GetFunds(Market.Asset)).FirstOrDefault();

      // setup buy order steps
      var steps = CalcBuySteps(basePrice,
        NumSpace.Linear(1.0m, OrdersUntilBelowBasePrice, MaxOrderCount),
        BuyStepSize)
        .OrderByDescending(x => x.Price);

      // calculate sum of all orders and check if the strategy is feasible
      //var amountSum = steps.Sum(x => x.Amount);
      //if (MaxQuote <= amountSum)
      //  throw new Exception($"Cannot setup Strategy because of insufficient funds (Available = {MaxQuote}, Necessary = {amountSum}[{Market.Quote}])");

      // enqueue steps and start with first
      foreach (var step in steps)
        BuySteps.Enqueue(step);
      await NextBuyStep();
    }

    private IEnumerable<BuyStep> CalcBuySteps(decimal price, IEnumerable<decimal> steps, decimal amount) {
      IList<BuyStep> stepList = new List<BuyStep>();
      foreach (var step in steps) {
        var priceStep = step * price;
        var quantity = amount / priceStep;
        stepList.Add(new BuyStep(priceStep, quantity));
      }
      return stepList;
    }

    private async Task NextBuyStep() {
      if (BuySteps.Count == 0) return;
      lock (locker) {
        if(AlreadyClearing) return;
      }

      do {
        try {
          if(BuySteps.TryDequeue(out BuyStep step))
            CurBuyOrder = await SetupOrder(step.Price, step.Quantity, OrderSide.BUY);
        } catch (Exception ex) {
          Log.Error(ex);
        }
      } while (BuySteps.Count > 0 && CurBuyOrder == null);
    }

    private async Task UpdateSellOrder(decimal price) {
      try {
        if (SellOrder != null) {
          await Market.Cancel(SellOrder);
          SellOrder = null;
        }

        for (int i = 0; i < 10 && SellOrder == null; ++i) {
          SellOrder = await SetupOrder(price, Quantity, OrderSide.SELL);
          price *= AvgMultiplier;
        }
      } catch (Exception ex) {
        Log.Error(ex.Message);
      }
    }

    private async Task<Order?> SetupOrder(decimal price, decimal quantity, OrderSide side) {
      if(Math.Round(price * quantity, Market.QuotePrecision) < Math.Round(BuyStepSize, Market.QuotePrecision)) {
        Log.Warn($"[SETUP ORDER {side} {Market.Asset}{Market.Quote} ERROR] -> " +
          $"MIN_NOMINAL Error = [price = {price}, quantity = {quantity}, amount = {price * quantity}], " +
          $"amount must be greater or equal '{BuyStepSize}'");
        return null;
      }
      Log.Info($"[SETUP ORDER {side} {Market.Asset}{Market.Quote}] -> {quantity} [{Market.Asset}] @ {price} [{Market.Quote}]");
      switch (side) {
        case OrderSide.BUY: return await Market.Buy(price, quantity);
        case OrderSide.SELL: return await Market.Sell(price, quantity);
        default: return null;
      }
    }

    private decimal AvgOrderWeight(Order order) =>
      order.Price * (order.ExecutedAmount / CurrentAmount);


    private async Task<IEnumerable<decimal>> GetFunds(params Symbol[] symbols) {
      var dict = await Market.Exchange.GetFunds();
      IList<decimal> funds = new List<decimal>();
      foreach (var symbol in symbols) {
        decimal fund;
        dict.TryGetValue(symbol, out fund);
        funds.Add(fund);
      }
      return funds;
    }

    private async Task Clear() {

      lock(locker) {
        if (AlreadyClearing) return;
        AlreadyClearing = true;
      }

      try {
        await Market.CancelAll();
        await CommisionManager.Add(Commission);
      } catch (Exception ex) { 
        Log.Error(ex);
      }
      lock(locker) {
        BuySteps.Clear();
        ExecutedOrders.Clear();
        IsTrading = false;
        SellOrder = null;
        CurBuyOrder = null;
        //Quantity = 0m;
        CurrentAmount = 0m;
        ProfitCalculator.Add(Profit);
        Revenue = 0m;
        Commission = 0m;
        RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        AlreadyClearing = false;
      }
      Log.Info($"CLEAR {Market.Asset}{Market.Quote}");
    }

    private void PrintStats() { // TODO: thread safe static class for profits
      ProfitCalculator.PrintStats();
    }
  }
}
