// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Strategy;
//using PiTrade.Strategy;

var configPath = @"C:\Users\David\Documents\binanceConfig.json";
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");



var exchange = new BinanceExchange(key, secret);
var markets = exchange.AvailableMarkets;
var selectedMarket = exchange.GetMarket(Symbol.GALA, Symbol.USDT);
if(selectedMarket != null)
{
  var strategy = new MovingAverageStrategy(selectedMarket);
  await strategy.Run(CancellationToken.None);
}