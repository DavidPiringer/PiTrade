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
var commissionMarket = await exchange.GetMarket(Symbol.BNB, Symbol.BUSD);

if (commissionMarket != null) {
  CommissionManager.CommissionMarket = commissionMarket;
  CommissionManager.CommissionFee = client.CommissionFee;
  CommissionManager.BuyThreshold = 15m;
  await exchange.Subscribe(commissionMarket);

  // TODO: maxActiveGrids?
  // TODO: multiple grid trading strategies
  // TODO: add buffer to websocket send

  // Fluent API?
  // Order.For(S1, S2).At(Binance).Sell().Quantity(q).AsLimit().WithPrice(p).Execute()

  // Order.Sell(x, S1).For(y, S2).At(Binance).AsLimit().Execute()
  //       ^ Qty+Asset ^ Price+Base

  // TODO: SERVICE STUFF -> Branch
  // PiTrade Exchange Add --Type Binance -Secret abc -Key xyz Name
  // PiTrade GridTradingStrategy --Opt1 ...
  // yaml file?
  /*
 Exchange:
  Type: Binance
  Secret: ...
  Key: ...
   */
  
  var configs = JsonConvert.DeserializeObject<GridTradingStrategyConfig[]>(File.ReadAllText(args.Last()));
  if(configs != null) {
    foreach(var c in configs) {
      if(c.Asset != null && c.Quote != null) {
        var market = await exchange.GetMarket(new Symbol(c.Asset), new Symbol(c.Quote));
        if(market != null) {
          await exchange.Subscribe(market);
          var strategy = new GridTradingStrategy(market, c);
          strategy.Enable();
        }
      }
    }
  }
}

await exchange.Run(CancellationToken.None);

