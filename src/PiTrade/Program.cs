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
Strategy.CommissionMarket = commissionMarket;

tasks.Add(Start(exchange.GetMarket(Symbol.ETH, Symbol.USDT), 10m, 0.005m, 0.0025m, 20));
/*
tasks.Add(Start(exchange.GetMarket(Symbol.BTC, Symbol.USDT), 10m, 0.005m, 0.0025m, 20));
tasks.Add(Start(exchange.GetMarket(Symbol.LUNA, Symbol.USDT), 10m, 0.01m, 0.0035m, 10));
tasks.Add(Start(exchange.GetMarket(Symbol.ATOM, Symbol.USDT), 10m, 0.01m, 0.0035m, 10));
tasks.Add(Start(exchange.GetMarket(Symbol.SAND, Symbol.USDT), 10m, 0.02m, 0.0035m, 10));
tasks.Add(Start(exchange.GetMarket(Symbol.BADGER, Symbol.USDT), 10m, 0.02m, 0.0035m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.GLMR, Symbol.USDT), 10m, 0.02m, 0.0035m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.FTM, Symbol.USDT), 10m, 0.02m, 0.0035m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.SHIB, Symbol.USDT), 10m, 0.02m, 0.0035m, 5));
*/
//tasks.Add(Start(exchange.GetMarket(Symbol.LOKA, Symbol.USDT), 10m, 0.04m, 0.0035m, 5));

Task.WaitAll(tasks.ToArray());
//if (CommissionManager.AwaitTask != null)
  //await CommissionManager.AwaitTask;

Task Start(IMarket? market, decimal quotePerGrid, decimal buyGridDistance, decimal sellThreshold, int buyGridCount) {
  if(market != null) {
    var strategy = new GridTradingStrategy(market, quotePerGrid, buyGridDistance, sellThreshold, buyGridCount);
    return strategy.Run();
  }
  return Task.CompletedTask;
}