﻿// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Base;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy;
using PiTrade.Strategy.Util;

var configPath = args.FirstOrDefault(@"C:\Users\David\Documents\binanceConfig.json");
var config = JObject.Parse(File.ReadAllText(configPath));

var key = config["key"]?.ToString();
var secret = config["secret"]?.ToString();

if (key == null || secret == null)
  throw new Exception("Key or Secret is null.");

var client = new BinanceStreamAPIClient(key, secret);
var exchange = new Exchange(client);
var market = await exchange.GetMarket(Symbol.ETH, Symbol.USDT);
var commissionMarket = await exchange.GetMarket(Symbol.BNB, Symbol.USDT);
if (market != null && commissionMarket != null) {
  await exchange.Subscribe(market, commissionMarket);
  CommissionManager.CommissionMarket = commissionMarket;
  CommissionManager.CommissionFee = client.CommissionFee;
  CommissionManager.BuyThreshold = 15m;

  var strategy = new GridTradingStrategy(market, 10.0m, 2650m, 2550m, 20, 0.005m); // TODO: better grid calc between bounds (nicht die ränder verwenden) -> x% * (max-min)
  // TODO: gegenchecken der orders (!isFilled || !isCancelled) in zeitabständen (zwecks der sicherheit)
  //var strategy = new GridTradingStrategy(market, 20.0m, 0.7m, 0.67m, 10, 0.005m);
  strategy.Enable();
}

await exchange.Run(CancellationToken.None);

