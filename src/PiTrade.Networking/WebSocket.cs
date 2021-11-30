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

    private ClientWebSocket? Socket { get; set; }
    private CancellationTokenSource? CTS { get; set; }

    public bool IsConnected { get; private set; } = false;


    public WebSocket() { }


    public async Task<string?> NextMessage() {
      if (Socket == null || CTS == null || Socket.State != WebSocketState.Open)
        throw new InvalidOperationException("WebSocket is not connected. Call Connect(Uri) before.");

      var buffer = new byte[receiveBufferSize];
      string? msg = null;
      using (MemoryStream outputStream = new MemoryStream(receiveBufferSize)) {
        try {
          WebSocketReceiveResult receiveResult;
          do {
              receiveResult = await Socket.ReceiveAsync(buffer, CTS.Token);
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
      Socket = new ClientWebSocket();
      CTS = new CancellationTokenSource();
      await Socket.ConnectAsync(connectionUri, CTS.Token);
    }

    public async Task Disconnect() {
      IsConnected = false;
      if(Socket != null) {
        if (Socket.State == WebSocketState.Open && CTS != null) {
          CTS.CancelAfter(TimeSpan.FromSeconds(2));
          await Socket.CloseOutputAsync(WebSocketCloseStatus.Empty, "", CancellationToken.None);
          await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        Socket.Dispose();
        Socket = null;
      }
      if(CTS != null) {
        CTS.Dispose();
        CTS = null;
      }
    }

    #region Dispose
    private bool disposedValue;
    protected virtual void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
          Disconnect().Wait();
        }
        disposedValue = true;
      }
    }

    public void Dispose() {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
    #endregion
  }
}
