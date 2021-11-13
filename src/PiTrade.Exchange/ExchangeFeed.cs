using PiTrade.Exchange.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace PiTrade.Exchange
{
  public abstract class ExchangeFeed : IExchangeFeed
  {
    public abstract event OrderEvent? OnBuy;
    public abstract event OrderEvent? OnSell;
    public abstract event PriceUpdateEvent? OnPriceUpdate;

    protected ClientWebSocket WebSocket { get; private set; } = new ClientWebSocket();
    protected CancellationTokenSource CancellationTokenSource { get; private set; } = new CancellationTokenSource();

    protected Uri ConnectionUri { get; private set; }
    
    private static int ReceiveBufferSize { get; set; } = 8192;


    public ExchangeFeed(Uri connectionUri)
    {
      ConnectionUri = connectionUri;
    }

    public async Task Run(CancellationToken token)
    {
      await Connect();
      await await Task.Factory.StartNew(async () => {
        while (!token.IsCancellationRequested)
        {
          await HandleMessage(await NextMessage());
        }
      }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
      await Disconnect();
    }

    protected virtual async Task OnOpen() => await Task.CompletedTask;
    protected virtual async Task OnClose() => await Task.CompletedTask;
    protected abstract Task HandleMessage(string msg);

    protected virtual async Task<string> NextMessage()
    {
      if (WebSocket.State != WebSocketState.Open)
        await Connect();

      var token = CancellationTokenSource.Token;
      var buffer = new byte[ReceiveBufferSize];
      string msg = "";
      using (MemoryStream outputStream = new MemoryStream(ReceiveBufferSize))
      {
        WebSocketReceiveResult receiveResult;
        do
        {
          receiveResult = await WebSocket.ReceiveAsync(buffer, token);
          if (receiveResult.MessageType != WebSocketMessageType.Close)
            outputStream.Write(buffer, 0, receiveResult.Count);
        } while (!receiveResult.EndOfMessage);
        outputStream.Position = 0;

        using (StreamReader reader = new StreamReader(outputStream))
          msg = reader.ReadToEnd();
      }
      return msg;
    }

    private async Task Connect()
    {
      await WebSocket.ConnectAsync(ConnectionUri, CancellationTokenSource.Token);
      await OnOpen();
    }

    private async Task Disconnect()
    {
      if (WebSocket.State == WebSocketState.Open)
      {
        await OnClose();
        CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
        await WebSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
        await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
      }
      WebSocket.Dispose();
      CancellationTokenSource.Dispose();
    }

    #region Dispose
    private bool disposedValue;
    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          Disconnect().Wait();
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~ExchangeFeed()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
