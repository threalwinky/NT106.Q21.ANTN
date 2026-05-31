using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace client.Networking;

internal sealed partial class NetrixClient
{
    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);
        await SendPacketAsync(data, cancellationToken);
    }

    private Task SendBinaryAsync(byte[] payload, CancellationToken cancellationToken)
    {
        return SendPacketAsync(payload, cancellationToken);
    }

    private async Task SendPacketAsync(byte[] payload, CancellationToken cancellationToken)
    {
        if (_client is null || _stream is null || _client.Connected != true || _receiveLoop is null || _receiveLoop.IsCompleted)
        {
            throw new InvalidOperationException(GetConnectionUnavailableMessage());
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var client = _client;
            var stream = _stream;
            if (client is null || stream is null || client.Connected != true || _receiveLoop is null || _receiveLoop.IsCompleted)
            {
                throw new InvalidOperationException(GetConnectionUnavailableMessage());
            }

            try
            {
                await WritePacketAsync(stream, payload, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException or InvalidOperationException)
            {
                await HandleUnexpectedDisconnectAsync(client, "Connection to the Netrix TCP server was lost. Try joining the room again.");
                throw new InvalidOperationException("Connection to the Netrix TCP server was lost. Try joining the room again.", ex);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private string GetConnectionUnavailableMessage()
    {
        return string.IsNullOrWhiteSpace(_lastDisconnectDetail)
            ? "Client is not connected."
            : _lastDisconnectDetail;
    }
}
