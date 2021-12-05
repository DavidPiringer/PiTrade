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
CommissionManager.Market = commissionMarket;

tasks.Add(Start(exchange.GetMarket(Symbol.SOL, Symbol.USDT), 400m, 40m, 0.9m));
tasks.Add(Start(exchange.GetMarket(Symbol.ETH, Symbol.USDT), 400m, 40m, 0.96m));
tasks.Add(Start(exchange.GetMarket(Symbol.BTC, Symbol.USDT), 400m, 40m, 0.96m));
//tasks.Add(Start(exchange.GetMarket(Symbol.SAND, Symbol.USDT), 200m, 10m, 0.96m));
//tasks.Add(Start(exchange.GetMarket(Symbol.ETH, Symbol.USDT), 700m, 35m, 0.96m));

Task.WaitAll(tasks.ToArray());
if (CommissionManager.AwaitTask != null)
  await CommissionManager.AwaitTask;

Task Start(IMarket? market, decimal maxQuote, decimal buyStep, decimal low) {
  if(market != null) {
    var strategy = new MovingAverageStrategy(market, maxQuote, buyStep, low);
    return strategy.Run(CancellationToken.None);
  }
  return Task.CompletedTask;
}