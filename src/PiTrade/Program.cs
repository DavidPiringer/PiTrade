// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy;


var configPath = args.FirstOrDefault(@"C:\Users\David\Documents\binanceConfig.json");
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");


var exchange = new BinanceExchange(key, secret);
var market = exchange.GetMarket(Symbol.ETH, Symbol.USDT);
if(market != null) {
  var strategy = new GridTradingStrategy(market, 10.0m, 2600m, 2400m, 10, 0.005m);

}



exchange.Run(CancellationToken.None);


var tasks = new List<Task>();

//var commissionMarket = exchange.GetMarket(Symbol.BNB, Symbol.USDT);
//if (commissionMarket == null) {
//  Log.Error("Commission Market is null.");
//  return;
//}
//Strategy.CommissionMarket = commissionMarket;

//tasks.Add(Start(exchange.GetMarket(Symbol.ETH, Symbol.USDT), 10m, 0.01m, 0.005m, 5));
/*
tasks.Add(Start(exchange.GetMarket(Symbol.BTC, Symbol.USDT), 10m, 0.01m, 0.005m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.ADA, Symbol.USDT), 10m, 0.02m, 0.005m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.SOL, Symbol.USDT), 10m, 0.02m, 0.005m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.LUNA, Symbol.USDT), 10m, 0.02m, 0.005m, 5));
tasks.Add(Start(exchange.GetMarket(Symbol.ATOM, Symbol.USDT), 10m, 0.02m, 0.005m, 5));
*/
//Task.WaitAll(tasks.ToArray());

Task Start(IMarket? market, decimal quotePerGrid, decimal buyGridDistance, decimal sellThreshold, int buyGridCount) {
  if(market != null) {
    var strategy = new GridTradingStrategy(market, quotePerGrid, buyGridDistance, sellThreshold, buyGridCount);
  }
  return Task.CompletedTask;
}

