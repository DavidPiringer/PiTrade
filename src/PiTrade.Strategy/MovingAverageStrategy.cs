using PiTrade.Exchange;
using PiTrade.Exchange.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Strategy
{
  public class MovingAverageStrategy
  {
    private struct BuyStep
    {
      public decimal Price = 0.0m;
      public decimal Quantity = 0.0m;
      public BuyStep(decimal price, decimal quantity)
      {
        this.Price = price;
        this.Quantity = quantity;
      }
    }

    private readonly IExchange Exchange;
    private readonly IExchangeFeed Feed;
    private readonly Market Market;

    private bool IsTrading { get; set; } = false;
    private long TradeStart { get; set; }
    private Order? SellOrder { get; set; } = null;
    private Queue<BuyStep> BuySteps { get; } = new Queue<BuyStep>();

    public MovingAverageStrategy(IExchange exchange, Market market)
    {
      Exchange = exchange;
      Market = market;


      Feed = Exchange.GetFeed(Market); //TODO: Exchange & Feed soll das gleiche sein
      Feed.OnPriceUpdate += OnPriceUpdate;
      Feed.OnBuy += OnBuy;
      Feed.OnSell += OnSell;
    }

    public async Task Run(CancellationToken token)
    {
      try
      {
        await Feed.Run(token);
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
      finally
      {
        await Exchange.CancelAll(Market);
      }
    }

    private async Task OnPriceUpdate(decimal price)
    {
      if (!IsTrading)
      {
        await Task.Delay(TimeSpan.FromSeconds(10));
        IsTrading = true;
        TradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var fineOrderSteps = new decimal[] { 1.0m, 0.998m, 0.996m, 0.994m };
        List<BuyStep> steps = new List<BuyStep>();
        steps.AddRange(SetupBuyOrder(price, new decimal[] {  
          1.0m, 0.999m, 0.998m, 0.997m, 0.995m, 0.99m
        }, 15.0m, 2m));
        steps.AddRange(SetupBuyOrder(price, new decimal[] {
          0.995m
        }, 600.0m, 1m));
        foreach (var step in steps.OrderByDescending(x => x.Price))
          BuySteps.Enqueue(step);
        await NextBuyStep();
      }
      else if (IsTrading && SellOrder == null && (TradeStart + 120) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
      {
        Console.WriteLine("Restart because of inactivity.");
        IsTrading = false;
        SellOrder = null;
        await Exchange.CancelAll(Market);
      }
    }

    private async Task OnBuy(Order order)
    {
      var filledOrders = Exchange.ActiveOrders.Where(x => x.IsFilled);
      var quantity = filledOrders.Sum(x => x.ExecutedQuantity * 0.99925m);
      var maxAmount = filledOrders.Sum(x => x.Price * x.Quantity);
      var avg = filledOrders.Sum(x => x.Price * ((x.Price * x.Quantity) / maxAmount));
      var sellPrice = avg * 1.00275m;

      Console.WriteLine($"{quantity} * {sellPrice}");

      if (SellOrder != null)
        await Exchange.Cancel(SellOrder);
      SellOrder = await Exchange.Sell(Market, sellPrice, quantity);
      await NextBuyStep();
    }

    private async Task OnSell(Order order)
    {
      await Exchange.CancelAll(Market);
      IsTrading = false;
      SellOrder = null;
    }

    private IEnumerable<BuyStep> SetupBuyOrder(decimal price, decimal[] steps, decimal start, decimal power)
    {
      decimal pot = 1.0m;
      IList<BuyStep> stepList = new List<BuyStep>();
      foreach (var step in steps)
      {
        var priceStep = step * price;
        var quantity = (1.0m + (start * pot)) / priceStep;
        pot *= power;
        stepList.Add(new BuyStep(priceStep, quantity));
      }
      return stepList;
    }

    private async Task NextBuyStep()
    {
      var firstStep = BuySteps.Dequeue();
      await Exchange.Buy(Market, firstStep.Price, firstStep.Quantity);
    }

  }
}
