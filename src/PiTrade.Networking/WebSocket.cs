using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using PiTrade.Logging;

namespace PiTrade.Networking {
  public class WebSocket {
    private static readonly int receiveBufferSize = 8192;

    private readonly ClientWebSocket webSocket = new ClientWebSocket();
    private readonly CancellationTokenSource cts = new CancellationTokenSource();

    public bool IsConnected { get; private set; } = false;


    public WebSocket() { }


    public async Task<string> NextMessage() {
      if (webSocket.State != WebSocketState.Open)
        throw new InvalidOperationException("WebSocket is not connected. Call Connect(Uri) before.");

      var token = cts.Token;
      var buffer = new byte[receiveBufferSize];
      string msg = "";
      using (MemoryStream outputStream = new MemoryStream(receiveBufferSize)) {
        try {
          WebSocketReceiveResult receiveResult;
          do {
              receiveResult = await webSocket.ReceiveAsync(buffer, token);
              if (receiveResult.MessageType != WebSocketMessageType.Close)
                outputStream.Write(buffer, 0, receiveResult.Count);
          
          } while (!receiveResult.EndOfMessage);
          outputStream.Position = 0;

          using (StreamReader reader = new StreamReader(outputStream))
            msg = reader.ReadToEnd();
        } catch (WebSocketException ex) {
          Log.Error($"{ex.GetType().Name} - {ex.Message}");
        }
      }
      return msg;
    }

    /*
     * TODO: SendMessage()
     */

    public async Task Connect(Uri connectionUri) {
      IsConnected = true;
      await webSocket.ConnectAsync(connectionUri, cts.Token);
    }

    public async Task Disconnect() {
      IsConnected = false;
      if (webSocket.State == WebSocketState.Open) {
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        await webSocket.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
      }
      webSocket.Dispose();
      cts.Dispose();
    }

    #region Dispose
    private bool disposedValue;
    protected virtual void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
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

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
