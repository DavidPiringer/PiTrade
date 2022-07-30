// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Base;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.DTOs;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy;

//#if DEBUG
//var configPath = args.FirstOrDefault(@"C:\Users\David\Documents\binanceTestConfig.json");
//#else
var configPath = args.FirstOrDefault(@"C:\Users\David\Documents\binanceConfig.json");
//#endif
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");


var exchange = await BinanceExchange.Create(key, secret);
var markets = exchange.Markets;
var btcbusd = markets.Where(x => x.BaseAsset == Symbol.BTC && x.QuoteAsset == Symbol.BUSD).First();
var strategy = new MACDStrategy(btcbusd, 19m, 1m, true, TimeSpan.FromMinutes(1));
//var strategy = new SimpleFluctuationStrategy(btcbusd, 19m, 1m, 50m);
//var strategy = new StandardDeviationStrategy(btcbusd, 20m, 1m, 0m, TimeSpan.FromMinutes(1), 8);
strategy.Start();
Console.WriteLine("Press Enter to stop ...");
Console.ReadLine();


