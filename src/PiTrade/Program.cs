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
var market2 = exchange.GetMarket(Symbol.BTC, Symbol.USDT);
if(market != null) {
  //var strategy = new GridTradingStrategy(market, 10.0m, 2425m, 2415m, 10, 0.005m);
  //strategy.Enable();
}



await exchange.Run(CancellationToken.None);

