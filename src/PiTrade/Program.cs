// See https://aka.ms/new-console-template for more information
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PiTrade.Exchange;
using PiTrade.Exchange.Base;
using PiTrade.Exchange.Binance;
using PiTrade.Exchange.Entities;
using PiTrade.Exchange.Indicators;
using PiTrade.Logging;
using PiTrade.Strategy;
using PiTrade.Strategy.ConfigDTOs;
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

  File.WriteAllText("strategyConfig.json", JsonConvert.SerializeObject(new GridTradingStrategyConfig()));
  GridTradingStrategyConfig? strategyConfig = JsonConvert.DeserializeObject<GridTradingStrategyConfig>(File.ReadAllText(args.Last()));
  if(strategyConfig != null) {
    var strategy = new GridTradingStrategy(market, strategyConfig);
    strategy.Enable();
  } else {
    Log.Error("No related config for strategy found. Created a empty template named 'strategyConfig.json'"); 
    File.WriteAllText("strategyConfig.json", JsonConvert.SerializeObject(new GridTradingStrategyConfig()));
  }
  //var strategy = new GridTradingStrategy(market, 10.0m, 0.5m, 3050m, 2700m, 100, 0.005m, false); 
  
}

await exchange.Run(CancellationToken.None);

