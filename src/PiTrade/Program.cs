// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using PiTrade.Strategy;

var configPath = @"C:\Users\David\Documents\binanceConfig.json";
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");



var exchange = new BinanceExchange(key, secret);
var markets = exchange.AvailableMarkets;
var mana_usdt = exchange.GetMarket(Symbol.MANA, Symbol.USDT);
var tasks = new List<Task>();

tasks.Add(Start(exchange.GetMarket(Symbol.MANA, Symbol.USDT), 605m, 15m, 0.9m));
tasks.Add(Start(exchange.GetMarket(Symbol.ROSE, Symbol.USDT), 251m, 12.5m, 0.9m));
tasks.Add(Start(exchange.GetMarket(Symbol.SOL, Symbol.USDT), 251m, 12.5m, 0.9m));

Task.WaitAll(tasks.ToArray());

Task Start(IMarket? market, decimal maxQuote, decimal buyStep, decimal low) {
  if(market != null) {
    var strategy = new MovingAverageStrategy(market, maxQuote, buyStep, low);
    return strategy.Run(CancellationToken.None);
  }
  return Task.CompletedTask;
}