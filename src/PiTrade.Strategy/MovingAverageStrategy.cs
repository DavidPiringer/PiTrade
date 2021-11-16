//using PiTrade.Exchange;
//using PiTrade.Exchange.Entities;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace PiTrade.Strategy
//{
//  public class MovingAverageStrategy
//  {
//    private struct BuyStep
//    {
//      public decimal Price = 0.0m;
//      public decimal Quantity = 0.0m;
//      public BuyStep(decimal price, decimal quantity)
//      {
//        this.Price = price;
//        this.Quantity = quantity;
//      }
//      public override string ToString() => 
//        $"[Price = {Price}, Quantity = {Quantity}, Amount = {Price * Quantity}]";
//    }

//    #region Properties
//    private readonly IExchange Exchange;
//    private readonly IExchangeFeed Feed;
//    private readonly Market Market;

//    private bool IsTrading { get; set; } = false;
//    private long TradeStart { get; set; }
//    private long RestartTime { get; set; }
//    private Order? SellOrder { get; set; } = null;
//    private Order? CurBuyOrder { get; set; } = null;
//    private Queue<BuyStep> BuySteps { get; } = new Queue<BuyStep>();
//    private decimal RestQuantity { get; set; }
//    private decimal QuoteAvailable { get; set; }
//    private decimal Profit { get; set; } = 0m;
//    private long TradeCount { get; set; } = 0;
//    #endregion

//    #region Constructors
//    public MovingAverageStrategy(IExchange exchange, Market market)
//    {
//      Exchange = exchange;
//      Market = market;


//      Feed = Exchange.GetFeed(Market); //TODO: Exchange & Feed soll das gleiche sein
//      Feed.OnPriceUpdate += OnPriceUpdate;
//      Feed.OnBuy += OnBuy;
//      Feed.OnSell += OnSell;
//    }
//    #endregion

//    public async Task Run(CancellationToken token)
//    {
//      try
//      {
//        RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
//        await Feed.Run(token);
//      }
//      catch (Exception ex)
//      {
//        Console.WriteLine("############## ERROR ################");
//        Console.WriteLine(ex.Message);
//        Console.WriteLine("#####################################");
//      }
//      finally
//      {
//        foreach(var order in Exchange.ActiveOrders.Where(x => x.Side == OrderSide.BUY && !x.IsFilled))
//        {
//          await Exchange.Cancel(order);
//        }
//      }
//    }

//    private async Task OnPriceUpdate(decimal price)
//    {
//      if (!IsTrading && (RestartTime + 5) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
//      {
//        // init
//        IsTrading = true;
//        TradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
//        var funds = await GetFunds(Market.Asset, Market.Quote);
//        if (funds.Count() != 2)
//          throw new Exception("At least one symbol in market is not available.");

//        var tmp = QuoteAvailable;
//        RestQuantity = funds.FirstOrDefault();
//        QuoteAvailable = funds.LastOrDefault();
//        var profit = QuoteAvailable - tmp;
//        if (TradeCount > 0)
//          Profit += profit;
//        TradeCount++;
//        Console.WriteLine("##############################################################################");
//        Console.WriteLine($"New Quote Funds = {QuoteAvailable}");
//        Console.WriteLine($"Old Quote Funds = {tmp}");
//        Console.WriteLine($"Delta = {profit}");
//        Console.WriteLine($"Profit = {Profit}");
//        Console.WriteLine("##############################################################################");

//        // setup buy order steps
//        Console.WriteLine($"Base Price = {price}");
//        List<BuyStep> steps = new List<BuyStep>();

//        //teps.AddRange(SetupBuyOrders(price, LinSpace(1.0m, 0.9m, 10), 10.25m, 1.525m));
//        //steps.AddRange(SetupBuyOrders(price, LinSpace(1.0m, 0.96m, 20), 14.0m, 1.125m));
//        //steps.AddRange(SetupBuyOrders(price, LinSpace(1.0m, 0.98m, 5), 30.0m, 1.6m)); // 400
//        //steps.AddRange(SetupBuyOrders(price, LinSpace(0.98m, 0.80m, 46), 20.0m, 1.0m)); // 920
//        steps.AddRange(SetupBuyOrders(price, LinSpace(1.0m, 0.8m, 40), 35m, 1.0m)); // 920
//        var orderedSteps = steps.OrderByDescending(x => x.Price);


//        // calculate sum of all orders and check if the strategy is feasible
//        var amountSum = orderedSteps.Sum(x => x.Price * x.Quantity);
//        Console.WriteLine($"Sum of all planned orders = {amountSum}[{Market.Quote}]");
//        if (QuoteAvailable < amountSum)
//          throw new Exception($"Cannot setup Strategy because of insufficient funds (Available = {QuoteAvailable}, Necessary = {amountSum}[{Market.Quote}])");

//        // enqueue steps and start with first
//        foreach (var step in orderedSteps)
//          BuySteps.Enqueue(step);
//        await NextBuyStep();
//      }
//      else if (IsTrading 
//        && SellOrder == null 
//        && !Exchange.ActiveOrders.Any(x => x.ExecutedQuantity > 0) 
//        && (TradeStart + 10) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
//        && CurBuyOrder != null
//        && CurBuyOrder.Price <= (price * 0.99m))
//      {
//        Console.WriteLine("#### Restart because of inactivity. ####");
//        await Exchange.CancelAll(Market);
//        IsTrading = false;
//        BuySteps.Clear();
//        SellOrder = null;
//        CurBuyOrder = null;
//        RestQuantity = 0.0m;
//      }
//    }

//    private async Task OnBuy(Order order)
//    {
//      var orders = Exchange.ActiveOrders;
//      var filledOrders = orders.Where(x => /*x.IsFilled && */x.Side.ToString() == OrderSide.BUY.ToString());

//      var quantity = orders.Sum(x => {
//        var qty = 0.0m;
//        if (x.Side.ToString() == OrderSide.SELL.ToString())
//        {
//          Console.WriteLine($">>>>>>>>>> SELL ORDER {x} <<<<<<<<<<<<");
//          qty -= x.ExecutedQuantity;
//        }
//        else
//          qty += x.ExecutedQuantity;
//        return qty;        
//      });
//      //quantity += RestQuantity;

//      foreach (var o in filledOrders)
//        Console.WriteLine($"FILLED = {o}");

//      // calculate avg and sell price
//      var maxAmount = filledOrders.Sum(x => x.Price * x.ExecutedQuantity);
//      var avg = filledOrders.Sum(x => x.Price * ((x.Price * x.ExecutedQuantity) / maxAmount));
//      var sellPrice = avg * 1.003m;

//      Console.WriteLine($"SELL {quantity} [{Market.Asset}] for {sellPrice} [{Market.Quote}]");

//      // setup new sell order an next buy step
//      if (SellOrder != null)
//        await Exchange.Cancel(SellOrder);
//      SellOrder = await Exchange.Sell(Market, sellPrice, quantity);
//      await NextBuyStep();

//      Console.WriteLine($"[{order}]");
//      //Console.WriteLine($"New AveragePrice = {avg}, SellPrice = {sellPrice}");
//    }

//    private async Task OnSell(Order order)
//    {
//      await Exchange.CancelAll(Market);
//      IsTrading = false;
//      BuySteps.Clear();
//      SellOrder = null;
//      CurBuyOrder = null;
//      RestQuantity = 0.0m;
//      RestartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
//      Console.WriteLine($"[{order}]");
//    }

//    private IEnumerable<BuyStep> SetupBuyOrders(decimal price, IEnumerable<decimal> steps, decimal start, decimal power)
//    {
//      decimal pot = 1.0m;
//      IList<BuyStep> stepList = new List<BuyStep>();
//      foreach (var step in steps)
//      {
//        var priceStep = step * price;
//        var quantity = (start * pot) / priceStep;
//        //var quantity = start / priceStep;
//        pot *= power;
//        stepList.Add(new BuyStep(priceStep, quantity));
//      }
//      return stepList;
//    }

//    private async Task NextBuyStep()
//    {
//      if(BuySteps.Count > 0)
//      {
//        var firstStep = BuySteps.Dequeue();
//        CurBuyOrder = await Exchange.Buy(Market, firstStep.Price, firstStep.Quantity);
//      }
//    }

//    private async Task<IEnumerable<decimal>> GetFunds(params Symbol[] symbols)
//    {
//      var dict = await Exchange.GetFunds();

//      IList<decimal> funds = new List<decimal>(); 
//      foreach (var symbol in symbols)
//      {
//        decimal fund = 0.0m;
//        dict.TryGetValue(symbol.ToString(), out fund);
//        funds.Add(fund);
//      }
//      return funds;
//    }

//    private IEnumerable<decimal> LinSpace(decimal start, decimal end, int count)
//    {
//      var s = Math.Max(start, end);
//      var e = Math.Min(start, end);
//      var stepSize = (s - e) / count;
//      for (decimal i = s; i > e; i -= stepSize)
//        yield return i;
//    }

//  }
//}
