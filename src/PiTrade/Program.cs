// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy;
using PiTrade.Strategy.Util;

var configPath = @"C:\Users\David\Documents\binanceConfig.json";
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");



var exchange = new BinanceExchange(key, secret);
var tasks = new List<Task>();

var commissionMarket = exchange.GetMarket(Symbol.BNB, Symbol.USDT);
if (commissionMarket == null) {
  Log.Error("Commission Market is null.");
  return;
}

//CommisionManager.Market = exchange.GetMarket(Symbol.BNB, Symbol.USDT);

//tasks.Add(commissionMarket.Listen(o => Task.CompletedTask, o => Task.CompletedTask, p => Task.Run(() => Console.WriteLine(p)), CancellationToken.None));
//tasks.Add(Start(exchange.GetMarket(Symbol.COCOS, Symbol.USDT), 406m, 15m, 0.8m));
//tasks.Add(Start(exchange.GetMarket(Symbol.GALA, Symbol.USDT), 406m, 15m, 0.8m));
//tasks.Add(Start(exchange.GetMarket(Symbol.MITH, Symbol.USDT), 211m, 10.5m, 0.6m));
//tasks.Add(Start(exchange.GetMarket(Symbol.DREP, Symbol.USDT), 211m, 10.5m, 0.8m));
//tasks.Add(Start(exchange.GetMarket(Symbol.GTO, Symbol.USDT), 211m, 10.5m, 0.6m));
//tasks.Add(Start(exchange.GetMarket(Symbol.KEY, Symbol.USDT), 422m, 10.5m, 0.6m));


Task.WaitAll(tasks.ToArray());

Task Start(IMarket? market, decimal maxQuote, decimal buyStep, decimal low) {
  if(market != null) {
    var strategy = new MovingAverageStrategy(market, maxQuote, buyStep, low);
    return strategy.Run(CancellationToken.None);
  }
  return Task.CompletedTask;
}