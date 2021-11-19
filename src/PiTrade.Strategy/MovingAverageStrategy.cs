using PiTrade.Exchange;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Enums;
using PiTrade.Logging;
using PiTrade.Strategy.Domain;
using PiTrade.Strategy.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Strategy {
  public class MovingAverageStrategy {
    #region Properties
    private readonly IMarket Market;
    private readonly decimal CommissionFee = 0.00075m;
    private readonly decimal AvgMultiplier = 1.002125m;
    private readonly int RestartDelay = 2;


    private decimal MaxQuote { get; set; }
    public decimal BuyStepSize { get; }
    public int MaxOrderCount => (int)(MaxQuote / BuyStepSize);
    public decimal OrdersUntilBelowBasePrice { get; }


    private bool IsTrading { get; set; } = false;
    private long TradeStart { get; set; }
    private long RestartTime { get; set; }
    private Order? SellOrder { get; set; } = null;
    private Order? CurBuyOrder { get; set; } = null;
    private Queue<BuyStep> BuySteps { get; } = new Queue<BuyStep>();
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
      try {
        RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await Market.Listen(OnBuy, OnSell, OnPriceUpdate, token);
      } catch (Exception ex) {
        var orders = Market.ActiveOrders;
        foreach (var o in orders)
          Log.Info(o);
        Log.Error(ex.Message);
      } finally {
        foreach (var order in Market.ActiveOrders.Where(x => x.Side == OrderSide.BUY && !x.IsFilled)) {
          await Market.Cancel(order);
        }
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
          && CurBuyOrder.Price <= (price * 0.99m)) {
        await Clear();
      }
    }

    private async Task OnBuy(Order order) {
      var lastFill = order.Fills.LastOrDefault();
      Quantity += lastFill; /* TODO: add later if BNB is depleted - lastFill * CommissionFee;*/
      Commission += lastFill * order.Price * CommissionFee;
      CurrentAmount += lastFill * order.Price;
      Revenue -= lastFill * order.Price;

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
      Quantity += lastFill;
      Commission += lastFill * order.Price * CommissionFee;
      Revenue += lastFill * order.Price;

      if (order.IsFilled) {
        await Clear();
        PrintStats();
      }
    }

    private async Task SetupTrade(decimal basePrice) {
      IsTrading = true;
      TradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

      // setup buy order steps
      var steps = CalcBuySteps(basePrice,
        NumSpace.Linear(1.0m, OrdersUntilBelowBasePrice, MaxOrderCount),
        BuyStepSize)
        .OrderByDescending(x => x.Price);

      // calculate sum of all orders and check if the strategy is feasible
      var amountSum = steps.Sum(x => x.Amount);
      if (MaxQuote <= amountSum)
        throw new Exception($"Cannot setup Strategy because of insufficient funds (Available = {MaxQuote}, Necessary = {amountSum}[{Market.Quote}])");

      // enqueue steps and start with first
      foreach (var step in steps)
        BuySteps.Enqueue(step);
      await NextBuyStep();
    }

    private IEnumerable<BuyStep> CalcBuySteps(decimal price, IEnumerable<decimal> steps, decimal start) {
      IList<BuyStep> stepList = new List<BuyStep>();
      foreach (var step in steps) {
        var priceStep = step * price;
        var quantity = start / priceStep;
        stepList.Add(new BuyStep(priceStep, quantity));
      }
      return stepList;
    }

    private async Task NextBuyStep() {
      if (BuySteps.Count > 0) {
        var firstStep = BuySteps.Dequeue();
        CurBuyOrder = await Market.Buy(firstStep.Price, firstStep.Quantity);
      }
    }

    private async Task UpdateSellOrder(decimal price) {
      if (SellOrder != null)
        await Market.Cancel(SellOrder);
      SellOrder = await Market.Sell(price, Quantity);
      Log.Info($"[SELL] -> {Quantity} [{Market.Asset}] @ {price} [{Market.Quote}]");
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
      await Market.CancelAll();
      BuySteps.Clear();
      ExecutedOrders.Clear();
      IsTrading = false;
      SellOrder = null;
      CurBuyOrder = null;
      Quantity = 0m;
      CurrentAmount = 0m;
      ProfitCalculator.Add(Profit);
      Revenue = 0m;
      Commission = 0m;
      RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
      Log.Info("CLEAR");
    }

    private void PrintStats() { // TODO: thread safe static class for profits
      ProfitCalculator.PrintStats();
    }
  }
}
