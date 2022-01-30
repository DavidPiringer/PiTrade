using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PiTrade.Logging;

namespace PiTrade.Networking {
  public class WebSocket<T> where T : class {
    private static readonly int receiveBufferSize = 8192;
    private readonly Uri uri;
    private readonly Func<string, T?> transformFnc;

    private ClientWebSocket? Socket { get; set; }
    private CancellationTokenSource? CTS { get; set; }

    public WebSocket(Uri uri) : this(uri, s => JsonConvert.DeserializeObject<T>(s)) { }
    public WebSocket(Uri connectionUri, Func<string, T?> transformFnc) { 
      uri = connectionUri;
      this.transformFnc = transformFnc;
    }


    public async Task<(T? message, bool success)> NextMessage() {
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
        return (transformFnc(reader.ReadToEnd()), true);
      } catch (Exception ex) {
        Log.Error($"{ex.GetType().Name} - {ex.Message}");
        return (default(T), false);
      }
    }

    public async Task SendMessage(string message) {
      try {
        if (Socket == null || CTS == null || Socket.State != WebSocketState.Open)
          await Connect();

        if (Socket != null && CTS != null) {
          byte[] sendBytes = Encoding.UTF8.GetBytes(message);
          var sendBuffer = new ArraySegment<byte>(sendBytes);
          await Socket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, CTS.Token);
        }
      } catch (Exception ex) {
        Log.Error($"{ex.GetType().Name} - {ex.Message}");
      }
    }

    private async Task Connect() {
      Socket = new ClientWebSocket();
      CTS = new CancellationTokenSource();
      await Socket.ConnectAsync(uri, CTS.Token);
    }

    private async Task Disconnect() {
      if(Socket != null) {
        if (Socket.State == WebSocketState.Open && CTS != null) {
          CTS.CancelAfter(TimeSpan.FromSeconds(2));
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
