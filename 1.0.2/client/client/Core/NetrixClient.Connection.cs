using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;

namespace client.Core;

internal sealed partial class NetrixClient
{
    public async Task ConnectAsync(string serverUrl, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (HasHealthyConnection && string.Equals(_connectedUrl, serverUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await DisconnectCoreAsync();

            var socket = new ClientWebSocket();
            var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _socket = socket;
            _connectionCts = connectionCts;
            _connectedUrl = serverUrl;

            try
            {
                await socket.ConnectAsync(new Uri(serverUrl), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await DisconnectCoreAsync();
                throw new InvalidOperationException(FormatConnectionError(serverUrl, ex), ex);
            }

            StatusChanged?.Invoke($"Connected to {serverUrl}");
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(socket, connectionCts.Token), CancellationToken.None);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await DisconnectCoreAsync();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 64];
        using var messageStream = new MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                messageStream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await HandleUnexpectedDisconnectAsync(socket, "Server closed the connection.");
                        return;
                    }

                    messageStream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                var json = Encoding.UTF8.GetString(messageStream.ToArray());
                HandleServerMessage(json);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await HandleUnexpectedDisconnectAsync(socket, $"Connection to the Netrix server was lost. {ex.Message}");
        }
    }

    private async Task DisconnectCoreAsync()
    {
        CurrentSession = null;
        _currentDisplayName = null;
        ClearRoomSecurity();

        var connectionCts = _connectionCts;
        var socket = _socket;

        _connectionCts = null;
        _socket = null;
        _receiveLoop = null;
        _connectedUrl = null;

        if (connectionCts is not null)
        {
            try
            {
                connectionCts.Cancel();
            }
            catch
            {
            }
        }

        if (socket is not null)
        {
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch
            {
            }
            finally
            {
                socket.Dispose();
            }
        }

        connectionCts?.Dispose();
    }

    private async Task HandleUnexpectedDisconnectAsync(ClientWebSocket socket, string detail)
    {
        var hadSession = false;

        await _connectionLock.WaitAsync();
        try
        {
            if (!ReferenceEquals(_socket, socket))
            {
                return;
            }

            hadSession = CurrentSession is not null;
            await DisconnectCoreAsync();
        }
        finally
        {
            _connectionLock.Release();
        }

        if (hadSession)
        {
            RoomClosed?.Invoke(detail);
        }
    }

    private static string FormatConnectionError(string serverUrl, Exception exception)
    {
        var reasons = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message) && !reasons.Contains(current.Message, StringComparer.Ordinal))
            {
                reasons.Add(current.Message.Trim());
            }
        }

        var hint = exception switch
        {
            WebSocketException => "WebSocket handshake failed. Check that the server is online and `/ws` is exposed.",
            AuthenticationException => "TLS negotiation failed. Verify the WSS certificate or tunnel configuration.",
            HttpRequestException => "HTTP transport failed before the WebSocket upgrade. Check DNS, proxy, or tunnel reachability.",
            _ => "Unable to establish the Netrix connection.",
        };

        return reasons.Count == 0
            ? $"{hint} Target: {serverUrl}"
            : $"{hint} Target: {serverUrl}. Details: {string.Join(" | ", reasons)}";
    }
}
