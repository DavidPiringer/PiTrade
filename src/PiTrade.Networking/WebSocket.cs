using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;

namespace PiTrade.Networking {
  public class WebSocket {
    private static readonly int receiveBufferSize = 8192;
    private readonly Uri uri;

    private ClientWebSocket socket;
    private CancellationTokenSource cancellationTokenSource;

    private Action<WebSocket> onConnect;
    private Action<string> onMessage;
    private Action<Exception> onError;
    private Action<WebSocket> onDisconnect;

    public WebSocket(Uri connectionUri) { 
      uri = connectionUri;
      socket = new ClientWebSocket();
      cancellationTokenSource = new CancellationTokenSource();
      onConnect = (ws) => { };
      onMessage = (s) => { };
      onError = (e) => { };
      onDisconnect = (ws) => { };
    }

    public WebSocket OnConnect(Action<WebSocket> fnc) {
      onConnect = fnc;
      return this;
    }

    public WebSocket OnMessage(Action<string> fnc) {
      onMessage = fnc;
      return this;
    }

    public WebSocket OnError(Action<Exception> fnc) {
      onError = fnc;
      return this;
    }

    public WebSocket OnDisconnect(Action<WebSocket> fnc) {
      onDisconnect = fnc;
      return this;
    }

    public async Task SendMessage(string message) {
      if (socket.State != WebSocketState.Open)
        throw new InvalidOperationException("WebSocket is not connected!");

      try {
        byte[] sendBytes = Encoding.UTF8.GetBytes(message);
        var sendBuffer = new ArraySegment<byte>(sendBytes);
        await socket.SendAsync(sendBuffer, WebSocketMessageType.Text, true, cancellationTokenSource.Token);
      } catch (Exception ex) {
        onError(ex);
      }
    }

    public async Task Connect() {
      if (socket.State == WebSocketState.None) {
        await socket.ConnectAsync(uri, cancellationTokenSource.Token);
        onConnect(this);
        MessageLoop();
      }
    }

    private async Task Reconnect() {
      await Disconnect();
      socket.Dispose();
      cancellationTokenSource.Dispose();

      socket = new ClientWebSocket();
      cancellationTokenSource = new CancellationTokenSource();
      await Connect();
    }

    public async Task Disconnect() {
      if (socket.State == WebSocketState.Open) {
        onDisconnect(this);
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));
        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
      }
    }

    private void MessageLoop() => Task.Run(async () => {
      while (!cancellationTokenSource.IsCancellationRequested) {
        if (socket.State != WebSocketState.Open)
          await Reconnect();
        await NextMessage();
      }
    });

    private async Task NextMessage() {
      try {
        var buffer = new byte[receiveBufferSize];
        using MemoryStream outputStream = new(receiveBufferSize);
        WebSocketReceiveResult receiveResult;
        if (socket != null && cancellationTokenSource != null) do {
            receiveResult = await socket.ReceiveAsync(buffer, cancellationTokenSource.Token);
            if (receiveResult.MessageType != WebSocketMessageType.Close)
              outputStream.Write(buffer, 0, receiveResult.Count);
          } while (!receiveResult.EndOfMessage);
        outputStream.Position = 0;
        using StreamReader reader = new(outputStream);
        onMessage(reader.ReadToEnd());
      } catch (Exception ex) {
        onError(ex);
      }
    }

    #region IDisposable Members
    private bool disposedValue;
    protected virtual void Dispose(bool disposing) {
      if (!disposedValue) {
        if (disposing) {
          Disconnect().Wait();
          socket.Dispose();
          cancellationTokenSource.Dispose();
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
