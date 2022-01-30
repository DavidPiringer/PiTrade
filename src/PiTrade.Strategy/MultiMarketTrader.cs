using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Exchange;

namespace PiTrade.Strategy {
  public class MultiMarketTrader {
    private readonly IExchange exchange;
    private readonly CancellationTokenSource CTS;

    public bool IsRunning { get; private set; }

    public MultiMarketTrader(IExchange exchange) {
      this.exchange = exchange;
      CTS = new CancellationTokenSource();
    }

    private void OnMarketAdded(IMarket market) {
      //market.Connect();
      //market.Listen()
    }

    public void Run() {
      if (IsRunning) return;
      IsRunning = true;
      while (CTS.IsCancellationRequested)
        Cycle();
    }

    public Task RunAsync() => 
      Task.Factory.StartNew(Run, CTS.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    public void Stop() {
      CTS.Cancel();
      IsRunning = false;
    }

    
    private void Cycle() {
      //exchange.AvailableMarkets.Where(x => x.)
    }
  }
}
