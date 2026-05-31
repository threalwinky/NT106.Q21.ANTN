using System.Net.Sockets;
using System.Text;

namespace client.Networking;

internal sealed partial class NetrixClient
{
    private const int MaxTcpPacketBytes = 12 * 1024 * 1024;
    private const int TcpSocketBufferBytes = 4 * 1024 * 1024;

    public async Task ConnectAsync(string serverEndpoint, CancellationToken cancellationToken)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (HasHealthyConnection && string.Equals(_connectedUrl, serverEndpoint, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await DisconnectCoreAsync();

            var (host, port) = ParseTcpEndpoint(serverEndpoint);
            var client = new TcpClient
            {
                NoDelay = true,
                SendBufferSize = TcpSocketBufferBytes,
                ReceiveBufferSize = TcpSocketBufferBytes,
            };
            var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await client.ConnectAsync(host, port, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                client.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                client.Dispose();
                throw new InvalidOperationException(FormatConnectionError(serverEndpoint, ex), ex);
            }

            _client = client;
            _stream = client.GetStream();
            _connectionCts = connectionCts;
            _connectedUrl = serverEndpoint;
            _lastDisconnectDetail = null;

            StatusChanged?.Invoke($"Connected to {serverEndpoint}");
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(client, _stream, connectionCts.Token), CancellationToken.None);
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

    private async Task ReceiveLoopAsync(TcpClient client, NetworkStream stream, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var messageBytes = await ReadPacketAsync(stream, MaxTcpPacketBytes, cancellationToken);
                if (messageBytes.AsSpan(0, Math.Min(messageBytes.Length, SecureFramePacketCodec.Magic.Length))
                    .SequenceEqual(SecureFramePacketCodec.Magic))
                {
                    HandleBinaryServerMessage(messageBytes);
                    continue;
                }

                var json = Encoding.UTF8.GetString(messageBytes);
                HandleServerMessage(json);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await HandleUnexpectedDisconnectAsync(client, $"Connection to the Netrix server was lost. {ex.Message}");
        }
    }

    private async Task DisconnectCoreAsync()
    {
        CurrentSession = null;
        _currentDisplayName = null;
        ClearRoomSecurity();

        var connectionCts = _connectionCts;
        var stream = _stream;
        var client = _client;

        _connectionCts = null;
        _stream = null;
        _client = null;
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

        try
        {
            stream?.Dispose();
            client?.Dispose();
        }
        catch
        {
        }

        connectionCts?.Dispose();
        await Task.CompletedTask;
    }

    private async Task HandleUnexpectedDisconnectAsync(TcpClient client, string detail)
    {
        var hadSession = false;

        await _connectionLock.WaitAsync();
        try
        {
            if (!ReferenceEquals(_client, client))
            {
                return;
            }

            hadSession = CurrentSession is not null;
            await DisconnectCoreAsync();
            _lastDisconnectDetail = detail;
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

    private static (string Host, int Port) ParseTcpEndpoint(string endpoint)
    {
        var trimmed = endpoint.Trim();
        var separatorIndex = trimmed.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            throw new InvalidOperationException($"Invalid main-server TCP endpoint: {endpoint}");
        }

        var host = trimmed[..separatorIndex].Trim();
        if (!int.TryParse(trimmed[(separatorIndex + 1)..], out var port) || port <= 0 || port > 65535)
        {
            throw new InvalidOperationException($"Invalid main-server TCP port: {endpoint}");
        }

        return (host, port);
    }

    private static async Task WritePacketAsync(NetworkStream stream, byte[] payload, CancellationToken cancellationToken)
    {
        var header = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(payload.Length));
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<byte[]> ReadPacketAsync(NetworkStream stream, int maxSize, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, cancellationToken);
        var length = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header));
        if (length <= 0 || length > maxSize)
        {
            throw new InvalidOperationException("Invalid TCP packet size.");
        }

        var payload = new byte[length];
        await ReadExactAsync(stream, payload, cancellationToken);
        return payload;
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
            {
                throw new IOException("TCP connection closed.");
            }

            offset += read;
        }
    }

    private static string FormatConnectionError(string serverEndpoint, Exception exception)
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
            SocketException => "TCP socket connection failed. Check that the server is online and the port is open.",
            IOException => "TCP transport failed. Check firewall, NAT, or server reachability.",
            _ => "Unable to establish the Netrix TCP connection.",
        };

        return reasons.Count == 0
            ? $"{hint} Target: {serverEndpoint}"
            : $"{hint} Target: {serverEndpoint}. Details: {string.Join(" | ", reasons)}";
    }
}
