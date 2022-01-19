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


tasks.Add(Start(exchange.GetMarket(Symbol.ETH, Symbol.USDT), 25m));
/*
tasks.Add(Start(exchange.GetMarket(Symbol.SOL, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.BTC, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.ADA, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.LUNA, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.FTM, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.ATOM, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.DOT, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.DOGE, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.SHIB, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.ROSE, Symbol.USDT), 25m));
tasks.Add(Start(exchange.GetMarket(Symbol.GALA, Symbol.USDT), 25m));
*/
Task.WaitAll(tasks.ToArray());
//if (CommissionManager.AwaitTask != null)
  //await CommissionManager.AwaitTask;

Task Start(IMarket? market, decimal maxQuote) {
  if(market != null) {
    var strategy = new WaveSurferStrategy(market, maxQuote, false);
    return strategy.Run();
  }
  return Task.CompletedTask;
}