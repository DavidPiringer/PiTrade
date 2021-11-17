using PiTrade.Exchange;
using PiTrade.Exchange.Entities;
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

    private bool IsTrading { get; set; } = false;
    private long TradeStart { get; set; }
    private long RestartTime { get; set; }
    private Order? SellOrder { get; set; } = null;
    private Order? CurBuyOrder { get; set; } = null;
    private Queue<BuyStep> BuySteps { get; } = new Queue<BuyStep>();
    private decimal RestQuantity { get; set; }
    private decimal QuoteAvailable { get; set; }
    private decimal Profit { get; set; } = 0m;
    private long TradeCount { get; set; } = 0;

    public decimal MaxQuote { get; }
    public decimal MinQuote { get; }
    public int MaxOrderCount => (int)(MaxQuote / MinQuote);
    #endregion

    #region Constructors
    public MovingAverageStrategy(IMarket market, decimal maxQuote, decimal minQuote) {
      Market = market;
      MaxQuote = maxQuote;
      MinQuote = minQuote;
    }
    #endregion

    public async Task Run(CancellationToken token) {
      try {
        RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await UpdateStats();
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
      if (!IsTrading && (RestartTime + 2) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()) {
        // TODO: nur kleine intervalle bis stop -> bei stop sofort verkaufen?
        // init
        IsTrading = true;
        TradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // setup buy order steps
        List<BuyStep> steps = new List<BuyStep>();
        steps.AddRange(SetupBuyOrders(price, NumSpace.Linear(1.0m, 0.85m, 160), 10.10m, 1m));
        var orderedSteps = steps.OrderByDescending(x => x.Price);


        // calculate sum of all orders and check if the strategy is feasible
        var amountSum = orderedSteps.Sum(x => x.Price * x.Quantity);
        Console.WriteLine($"Sum of all planned orders = {amountSum}[{Market.Quote}]");
        if (QuoteAvailable < amountSum)
          throw new Exception($"Cannot setup Strategy because of insufficient funds (Available = {QuoteAvailable}, Necessary = {amountSum}[{Market.Quote}])");

        // enqueue steps and start with first
        foreach (var step in orderedSteps)
          BuySteps.Enqueue(step);
        await NextBuyStep();
      } else if (IsTrading
          && SellOrder == null
          && !Market.ActiveOrders.Any(x => x.ExecutedQuantity > 0)
          && (TradeStart + 10) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
          && CurBuyOrder != null
          && CurBuyOrder.Price <= (price * 0.99m)) {
        await Restart();
      }
    }

    private async Task OnBuy(Order order) {
      if (!order.IsFilled) return;

      var orders = Market.ActiveOrders;
      var filledOrders = orders.Where(x => /*x.IsFilled && */x.Side.ToString() == OrderSide.BUY.ToString());

      var quantity = orders.Sum(x => {
        var qty = 0.0m;
        if (x.Side.ToString() == OrderSide.SELL.ToString())
          qty -= x.ExecutedQuantity;
        else
          qty += x.ExecutedQuantity;
        return qty;
      });


      // calculate avg and sell price
      var maxAmount = filledOrders.Sum(x => x.Price * x.ExecutedQuantity);
      var avg = filledOrders.Sum(x => x.Price * ((x.Price * x.ExecutedQuantity) / maxAmount));
      var sellPrice = avg * 1.002125m; //TODO threshold shift array [1.05m, 1.025m ..]

      Console.WriteLine($"SELL {quantity} [{Market.Asset}] for {sellPrice} [{Market.Quote}]");

      // setup new sell order an next buy step
      if (SellOrder != null)
        await Market.Cancel(SellOrder);
      SellOrder = await Market.Sell(sellPrice, quantity);
      await NextBuyStep();
    }

    private async Task OnSell(Order order) {
      if (!order.IsFilled) return;
      await Restart();
      await UpdateStats();
    }

    private IEnumerable<BuyStep> SetupBuyOrders(decimal price, IEnumerable<decimal> steps, decimal start, decimal power) {
      decimal pot = 1.0m;
      IList<BuyStep> stepList = new List<BuyStep>();
      foreach (var step in steps) {
        var priceStep = step * price;
        var quantity = (start * pot) / priceStep;
        pot *= power;
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

    private async Task Restart() {
      Console.WriteLine("############ RESTART ############");
      await Market.CancelAll();
      IsTrading = false;
      BuySteps.Clear();
      SellOrder = null;
      CurBuyOrder = null;
      RestQuantity = 0.0m;
      RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private async Task UpdateStats() {
      var funds = await GetFunds(Market.Asset, Market.Quote);
      if (funds.Count() != 2)
        throw new Exception("At least one symbol in market is not available.");
      var tmp = QuoteAvailable;
      RestQuantity = funds.FirstOrDefault();
      QuoteAvailable = funds.LastOrDefault();
      var profit = QuoteAvailable - tmp;
      if (TradeCount > 0)
        Profit += profit;
      TradeCount++;
      Console.WriteLine("##############################################################################");
      Console.WriteLine($"New Quote Funds = {QuoteAvailable}");
      Console.WriteLine($"Old Quote Funds = {tmp}");
      Console.WriteLine($"Delta = {profit}");
      Console.WriteLine($"Profit = {Profit}");
      Console.WriteLine("##############################################################################");
    }
  }
}
