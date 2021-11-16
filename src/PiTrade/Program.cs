// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
//using PiTrade.Strategy;

var configPath = @"C:\Users\David\Documents\binanceConfig.json";
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");



//var market = new Market(Symbol.BNB, Symbol.EUR, 3, 1);
//var market = new Market(Symbol.SHIB, Symbol.EUR, 0, 8);
//var market = new Market(Symbol.WIN, Symbol.EUR, 0, 7);
//var market = new Market(Symbol.SXP, Symbol.EUR, 1, 3);
//var market = new Market(Symbol.PORTO, Symbol.EUR, 2, 4);
//var market = new Market(Symbol.GALA, Symbol.USDT, 0, 5);
//var market = new Market(Symbol.ADA, Symbol.EUR, 1, 3);

//Console.WriteLine(OrderSide.BUY);

var exchange = new BinanceExchange(key, secret);
var markets = exchange.AvailableMarkets;
var selectedMarket = exchange.GetMarket(Symbol.GALA, Symbol.USDT);
if(selectedMarket != null)
{
  await selectedMarket.Listen(
    o => Task.CompletedTask,
    o => Task.CompletedTask,
    p => Task.Run(() => Console.WriteLine(p)),
    CancellationToken.None);
}

//await exchange.AvailableMarkets.First().Listen(o => Task.CompletedTask, o => Task.CompletedTask, o => Task.CompletedTask, CancellationToken.None);

//await exchange.GetExchangeInformation(market);

//var strategy = new MovingAverageStrategy(exchange, market);
//await strategy.Run(CancellationToken.None);
