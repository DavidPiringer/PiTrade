// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Strategy;

var configPath = @"C:\Users\David\Documents\binanceConfig.json";
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");



var market = new Market(Symbol.BNB, Symbol.EUR, 3, 1);
var exchange = new BinanceExchange(key, secret);

var strategy = new MovingAverageStrategy(exchange, market);
await strategy.Run(CancellationToken.None);

/*
var isTrading = false;
var tradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var fineOrderSteps = new decimal[] { 1.0m, 0.998m, 0.996m, 0.994m };
var fineOrderSteps2 = new decimal[] { 0.999m, 0.997m, 0.995m };
var grainOrderSteps = new decimal[] { 0.99m, 0.98m, 0.97m, 0.96m, 0.95m };
Order? sellOrder = null;

var locker = new object();


await exchange.GetExchangeInformation(market);
var funds = await exchange.GetFunds();
decimal eurosAvailable = 0.0m;
decimal bnbAvailable = 0.0m;

if (!funds.TryGetValue(Symbol.EUR.ToString(), out eurosAvailable))
  throw new Exception("No Euro Balance");
if (!funds.TryGetValue(Symbol.BNB.ToString(), out bnbAvailable))
  throw new Exception("No BNB Balance");


feed.OnSell += async (order) => {
  await exchange.CancelAll(market);
  isTrading = false; // TODO: check if sell order is filled
  sellOrder = null;
  var tmp = eurosAvailable;
  funds = await exchange.GetFunds();
  if (!funds.TryGetValue(Symbol.EUR.ToString(), out eurosAvailable))
    throw new Exception("No Euro Balance");
  Console.WriteLine($"{eurosAvailable} - {tmp} = {eurosAvailable - tmp}");
};

feed.OnBuy += async (order) => {
  var isTradingTmp = false;
  lock(locker)
    isTradingTmp = isTrading;
  if (isTradingTmp)
  {
    var filledOrders = exchange.ActiveOrders.Where(x => x.IsFilled);
    var quantity = filledOrders.Sum(x => x.ExecutedQuantity * 0.99925m);
    var maxAmount = filledOrders.Sum(x => x.Price * x.Quantity);
    var avg = filledOrders.Sum(x => x.Price * ((x.Price * x.Quantity) / maxAmount));
    var sellPrice = avg * 1.00275m;

    Console.WriteLine($"{quantity} * {sellPrice}");

    if (sellOrder != null)
      await exchange.Cancel(sellOrder);
    sellOrder = await exchange.Sell(market, sellPrice, quantity);
  }
};

feed.OnPriceUpdate += async (price) =>
{
  if (!isTrading) {
    await Task.Delay(TimeSpan.FromSeconds(30));
    isTrading = true;
    tradeStart = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    await SetupBuyOrder(exchange, market, price, fineOrderSteps, 30.0m, 2.25m);
    await SetupBuyOrder(exchange, market, price, fineOrderSteps2, 50.0m, 1.0m);
    await SetupBuyOrder(exchange, market, price, grainOrderSteps, 140.0m, 1.0m);
  } else if(isTrading && sellOrder == null && (tradeStart + 120) <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
  {
    Console.WriteLine("Restart because of inactivity.");
    lock (locker)
    {
      isTrading = false;
      sellOrder = null;
    }
    await exchange.CancelAll(market);
  }
};

try
{
  await feed.Run(CancellationToken.None);
} catch (Exception ex)
{
  Console.WriteLine(ex.Message);
} finally
{
  await exchange.CancelAll(market);
}


static async Task SetupBuyOrder(IExchange exchange, Market market, decimal price, decimal[] steps, decimal start, decimal power)
{
  decimal pot = 1.0m;
  foreach (var step in steps)
  {
    var priceStep = step * price;
    var quantity = (1.0m + (start * pot)) / priceStep;
    pot *= power;
    await exchange.Buy(market, priceStep, quantity);
  }
}
*/