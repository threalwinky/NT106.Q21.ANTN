using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace client.Core;

internal sealed partial class NetrixClient
{
    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        if (_socket is null || _socket.State != WebSocketState.Open || _receiveLoop is null || _receiveLoop.IsCompleted)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var socket = _socket;
            if (socket is null || socket.State != WebSocketState.Open || _receiveLoop is null || _receiveLoop.IsCompleted)
            {
                throw new InvalidOperationException("Client is not connected.");
            }

            try
            {
                await socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException)
            {
                await HandleUnexpectedDisconnectAsync(socket, "Connection to the Netrix server was lost. Try joining the room again.");
                throw new InvalidOperationException("Connection to the Netrix server was lost. Try joining the room again.", ex);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
