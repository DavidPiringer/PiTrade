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
    private readonly Uri uri;

    private ClientWebSocket? Socket { get; set; }
    private CancellationTokenSource? CTS { get; set; }

    public WebSocket(Uri connectionUri) { 
      uri = connectionUri;
    }


    public async Task<(string? message, bool success)> NextMessage() {
      try {
        if (Socket == null || CTS == null || Socket.State != WebSocketState.Open)
          await Connect();

        var buffer = new byte[receiveBufferSize];
        using MemoryStream outputStream = new(receiveBufferSize);
        WebSocketReceiveResult receiveResult;
        if (Socket != null && CTS != null) do {
          receiveResult = await Socket.ReceiveAsync(buffer, CTS.Token);
          if (receiveResult.MessageType != WebSocketMessageType.Close)
            outputStream.Write(buffer, 0, receiveResult.Count);
        } while (!receiveResult.EndOfMessage);
        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        return (reader.ReadToEnd(), true);
      } catch (Exception ex) {
        Log.Error($"{ex.GetType().Name} - {ex.Message}");
        return (null, false);
      }
    }

    /*
     * TODO: SendMessage()
     */

    public async Task Connect() {
      Socket = new ClientWebSocket();
      CTS = new CancellationTokenSource();
      await Socket.ConnectAsync(uri, CTS.Token);
    }

    public async Task Disconnect() {
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
