// See https://aka.ms/new-console-template for more information
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;


var market = new Market(Symbol.BNB, Symbol.EUR, 3, 1);
var exchange = new BinanceExchange("vitcGjqh6TjZ2bFLhJWKX2ydhiCDEOPOSwhQgLK3fxpgDm9s5kmKjM7RFLobNDAw", "3C3CATpLpI3FwwlVi25ndzZQ0aep0EgAhz650vlNsK3z9OoqiDIMl7P7YK1oudzI");
var feed = exchange.GetFeed(market);

var isTrading = false;
var fineOrderSteps = new decimal[] { 1.0m, 0.999m, 0.998m, 0.9965m, 0.995m };
var grainOrderSteps = new decimal[] { 0.99m, 0.98m, 0.97m, 0.96m, 0.95m };

await exchange.GetExchangeInformation(market);

feed.OnSell += async (order) => {
  await Task.CompletedTask;
};

feed.OnBuy += async (order) => {
  await Task.CompletedTask;
};

feed.OnPriceUpdate += async (price) =>
{
  if (!isTrading) {
    isTrading = true;
    //await SetupBuyOrder(exchange, market, price, fineOrderSteps, 10.0m, 2.5m);
    await SetupBuyOrder(exchange, market, price, grainOrderSteps, 140.0m, 1.0m);
  }
};

await feed.Run(CancellationToken.None);

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