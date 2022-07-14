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

var configPath = args.FirstOrDefault(@"C:\Users\David\Documents\binanceTestConfig.json");
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");


var exchange = await BinanceExchange.Create(key, secret);
var markets = exchange.Markets;
var btcbusd = markets.Where(x => x.BaseAsset == Symbol.BTC && x.QuoteAsset == Symbol.BUSD).First();
var order1 = await btcbusd.Sell(0.01m)
  .OnTrade((o, t) => {
    global::System.Console.WriteLine(o);
  })
  .OnExecuted(o => {
    global::System.Console.WriteLine($"Executed {o.Id}");
  })
  .Submit();

var order2 = await btcbusd
  .Buy(0.01m)
  .For(25000m)
  .OnTrade((o, t) => {
    global::System.Console.WriteLine(o);
  })
  .OnExecuted(o => {
    global::System.Console.WriteLine($"Executed {o.Id}");
  })
  .Submit();

Console.ReadLine();


