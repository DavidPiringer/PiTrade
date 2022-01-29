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

/*
 * TODO:
 * - Add IOrder interface .. done
 * - merge BinanceMarket & BinanceExchange -> BinanceAPI
 * - move BaseClasses into separate folder
 * - split Exchange BaseClass -> BasicExchange / Exchange
 * - Add GenericParameter for Exchange BaseClasses
 *   - BasicExchange -> T : IExchangeAPI
 *   - Exchange -> T: IExchangeAPI, IExchangeStream / Listener
 * - Remove Connect/Disconnect from Market ???
 * 
 */
var exchange = new BinanceExchange(key, secret);
var market = exchange.GetMarket(Symbol.ETH, Symbol.USDT);
if(market != null) {
  var strategy = new GridTradingStrategy(market, 30.0m, 2640m, 2620m, 10, 0.005m); // TODO: better grid calc between bounds (nicht die ränder verwenden) -> x% * (max-min)
  // TODO: gegenchecken der orders (!isFilled || !isCancelled) in zeitabständen (zwecks der sicherheit)
  //var strategy = new GridTradingStrategy(market, 20.0m, 0.7m, 0.67m, 10, 0.005m);
  strategy.Enable();
}



await exchange.Run(CancellationToken.None);

