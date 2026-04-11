using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace client.Core;

internal sealed class NetrixClient : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly RoomSecurityContext _roomSecurity = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveLoop;
    private string? _connectedUrl;
    private string? _currentDisplayName;

    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorReceived;
    public event Action<RoomSessionInfo>? RoomReady;
    public event Action<IReadOnlyList<ParticipantInfo>>? ParticipantsUpdated;
    public event Action<RemoteFrame>? FrameReceived;
    public event Action<ChatMessage>? ChatReceived;
    public event Action<RemoteInputCommand>? InputReceived;
    public event Action<FileTransferOffer>? FileOfferReceived;
    public event Action<FileTransferChunk>? FileChunkReceived;
    public event Action<FileTransferComplete>? FileTransferCompleted;
    public event Action<string>? RoomClosed;

    public bool IsConnected => _socket?.State == WebSocketState.Open;

    public RoomSessionInfo? CurrentSession { get; private set; }

    public async Task ConnectAsync(string serverUrl, CancellationToken cancellationToken)
    {
        if (IsConnected && string.Equals(_connectedUrl, serverUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await DisconnectAsync();

        _socket = new ClientWebSocket();
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _connectedUrl = serverUrl;
        await _socket.ConnectAsync(new Uri(serverUrl), cancellationToken);
        StatusChanged?.Invoke($"Connected to {serverUrl}");
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_connectionCts.Token), _connectionCts.Token);
    }

    public async Task DisconnectAsync()
    {
        CurrentSession = null;
        _currentDisplayName = null;
        ClearRoomSecurity();

        if (_connectionCts is not null)
        {
            _connectionCts.Cancel();
            _connectionCts.Dispose();
            _connectionCts = null;
        }

        if (_socket is not null)
        {
            try
            {
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch
            {
            }

            _socket.Dispose();
            _socket = null;
        }
    }

    public Task CreateRoomAsync(string displayName, string roomPassword, AppMode mode, string? token, CancellationToken cancellationToken)
    {
        _currentDisplayName = displayName;
        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "create_room",
                ["display_name"] = displayName,
                ["room_password"] = roomPassword,
                ["access_mode"] = mode.ToString().ToLowerInvariant(),
                ["token"] = token,
            },
            cancellationToken);
    }

    public Task JoinRoomAsync(string roomId, string displayName, string roomPassword, ParticipantRole role, AppMode mode, string? token, CancellationToken cancellationToken)
    {
        _currentDisplayName = displayName;
        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "join_room",
                ["room_id"] = roomId,
                ["display_name"] = displayName,
                ["room_password"] = roomPassword,
                ["role"] = role.ToString().ToLowerInvariant(),
                ["access_mode"] = mode.ToString().ToLowerInvariant(),
                ["token"] = token,
            },
            cancellationToken);
    }

    public Task SendFrameAsync(RemoteFrame frame, CancellationToken cancellationToken)
    {
        if (_roomSecurity.IsEnabled)
        {
            return SendSecurePayloadAsync(
                "frame",
                new
                {
                    jpeg_base64 = Convert.ToBase64String(frame.JpegBytes),
                    width = frame.Width,
                    height = frame.Height,
                    sent_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                },
                cancellationToken);
        }

        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "frame",
                ["jpeg_base64"] = Convert.ToBase64String(frame.JpegBytes),
                ["width"] = frame.Width,
                ["height"] = frame.Height,
                ["sent_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            cancellationToken);
    }

    public Task SendInputAsync(RemoteInputCommand command, CancellationToken cancellationToken)
    {
        if (_roomSecurity.IsEnabled)
        {
            return SendSecurePayloadAsync(
                "input",
                new
                {
                    @event = command.EventName,
                    x_ratio = command.XRatio,
                    y_ratio = command.YRatio,
                    button = command.Button,
                    delta = command.Delta,
                    key_code = command.KeyCode,
                },
                cancellationToken);
        }

        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "input",
                ["event"] = command.EventName,
                ["x_ratio"] = command.XRatio,
                ["y_ratio"] = command.YRatio,
                ["button"] = command.Button,
                ["delta"] = command.Delta,
                ["key_code"] = command.KeyCode,
            },
            cancellationToken);
    }

    public Task SendChatAsync(string text, CancellationToken cancellationToken)
    {
        if (_roomSecurity.IsEnabled)
        {
            return SendSecurePayloadAsync(
                "chat",
                new
                {
                    sender = _currentDisplayName ?? "Unknown",
                    role = CurrentSession?.Role.ToString().ToLowerInvariant() ?? "unknown",
                    text,
                },
                cancellationToken);
        }

        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "chat",
                ["text"] = text,
            },
            cancellationToken);
    }

    public Task SendFileOfferAsync(FileTransferOffer offer, CancellationToken cancellationToken)
    {
        var payload = new
        {
            transfer_id = offer.TransferId,
            file_name = offer.FileName,
            file_size = offer.FileSize,
            total_chunks = offer.TotalChunks,
            sender_name = offer.SenderName,
            sender_client_id = offer.SenderClientId,
        };

        return _roomSecurity.IsEnabled
            ? SendSecurePayloadAsync("file_offer", payload, cancellationToken)
            : SendAsync(
                new Dictionary<string, object?>
                {
                    ["type"] = "file_offer",
                    ["transfer_id"] = offer.TransferId,
                    ["file_name"] = offer.FileName,
                    ["file_size"] = offer.FileSize,
                    ["total_chunks"] = offer.TotalChunks,
                    ["sender_name"] = offer.SenderName,
                    ["sender_client_id"] = offer.SenderClientId,
                },
                cancellationToken);
    }

    public Task SendFileChunkAsync(FileTransferChunk chunk, CancellationToken cancellationToken)
    {
        var payload = new
        {
            transfer_id = chunk.TransferId,
            chunk_index = chunk.ChunkIndex,
            total_chunks = chunk.TotalChunks,
            chunk_base64 = Convert.ToBase64String(chunk.ChunkBytes),
        };

        return _roomSecurity.IsEnabled
            ? SendSecurePayloadAsync("file_chunk", payload, cancellationToken)
            : SendAsync(
                new Dictionary<string, object?>
                {
                    ["type"] = "file_chunk",
                    ["transfer_id"] = chunk.TransferId,
                    ["chunk_index"] = chunk.ChunkIndex,
                    ["total_chunks"] = chunk.TotalChunks,
                    ["chunk_base64"] = Convert.ToBase64String(chunk.ChunkBytes),
                },
                cancellationToken);
    }

    public Task SendFileCompleteAsync(FileTransferComplete complete, CancellationToken cancellationToken)
    {
        var payload = new
        {
            transfer_id = complete.TransferId,
        };

        return _roomSecurity.IsEnabled
            ? SendSecurePayloadAsync("file_complete", payload, cancellationToken)
            : SendAsync(
                new Dictionary<string, object?>
                {
                    ["type"] = "file_complete",
                    ["transfer_id"] = complete.TransferId,
                },
                cancellationToken);
    }

    public Task PingAsync(CancellationToken cancellationToken)
    {
        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "ping",
            },
            cancellationToken);
    }

    public void ConfigureRoomSecurity(string roomId, string roomPassword, bool enabled)
    {
        _roomSecurity.Configure(roomId, roomPassword, enabled);
    }

    public void ClearRoomSecurity()
    {
        _roomSecurity.Clear();
    }

    private Task SendSecurePayloadAsync(string channel, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var encrypted = _roomSecurity.EncryptJson(channel, json);

        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "secure_payload",
                ["channel"] = channel,
                ["nonce_base64"] = encrypted.NonceBase64,
                ["ciphertext_base64"] = encrypted.CiphertextBase64,
            },
            cancellationToken);
    }

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
        if (_socket is null || _socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Client is not connected.");
        }

        var json = JsonSerializer.Serialize(payload);
        var data = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        var buffer = new byte[1024 * 64];
        using var messageStream = new MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                messageStream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        RoomClosed?.Invoke("Server closed the connection.");
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
            ErrorReceived?.Invoke(ex.Message);
        }
    }

    private void HandleServerMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var messageType = root.GetProperty("type").GetString() ?? string.Empty;

        switch (messageType)
        {
            case "hello":
                StatusChanged?.Invoke(root.GetProperty("message").GetString() ?? "Connected");
                break;
            case "error":
                ErrorReceived?.Invoke(root.GetProperty("detail").GetString() ?? "Unknown error");
                break;
            case "room_created":
                CurrentSession = new RoomSessionInfo(
                    root.GetProperty("room_id").GetString() ?? string.Empty,
                    root.GetProperty("client_id").GetString() ?? string.Empty,
                    ParseRole(root.GetProperty("role").GetString()),
                    ParseMode(root.GetProperty("access_mode").GetString()));
                RoomReady?.Invoke(CurrentSession);
                break;
            case "joined_room":
                CurrentSession = new RoomSessionInfo(
                    root.GetProperty("room_id").GetString() ?? string.Empty,
                    root.GetProperty("client_id").GetString() ?? string.Empty,
                    ParseRole(root.GetProperty("role").GetString()),
                    ParseMode(root.GetProperty("access_mode").GetString()),
                    root.TryGetProperty("host_client_id", out var hostClientId) ? hostClientId.GetString() : null);
                RoomReady?.Invoke(CurrentSession);
                break;
            case "room_state":
                ParticipantsUpdated?.Invoke(ParseParticipants(root));
                break;
            case "frame":
                var bytes = Convert.FromBase64String(root.GetProperty("jpeg_base64").GetString() ?? string.Empty);
                FrameReceived?.Invoke(new RemoteFrame(bytes, root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32()));
                break;
            case "chat":
                ChatReceived?.Invoke(
                    new ChatMessage(
                        root.GetProperty("sender").GetString() ?? "Unknown",
                        root.GetProperty("role").GetString() ?? string.Empty,
                        root.GetProperty("text").GetString() ?? string.Empty));
                break;
            case "file_offer":
                FileOfferReceived?.Invoke(ParseFileOffer(root));
                break;
            case "file_chunk":
                FileChunkReceived?.Invoke(ParseFileChunk(root));
                break;
            case "file_complete":
                FileTransferCompleted?.Invoke(ParseFileComplete(root));
                break;
            case "input":
                InputReceived?.Invoke(
                    new RemoteInputCommand(
                        root.GetProperty("event").GetString() ?? string.Empty,
                        root.TryGetProperty("x_ratio", out var xRatio) && xRatio.ValueKind != JsonValueKind.Null ? xRatio.GetDouble() : null,
                        root.TryGetProperty("y_ratio", out var yRatio) && yRatio.ValueKind != JsonValueKind.Null ? yRatio.GetDouble() : null,
                        root.TryGetProperty("button", out var button) ? button.GetString() : null,
                        root.TryGetProperty("delta", out var delta) && delta.ValueKind != JsonValueKind.Null ? delta.GetInt32() : null,
                        root.TryGetProperty("key_code", out var keyCode) && keyCode.ValueKind != JsonValueKind.Null ? keyCode.GetInt32() : null));
                break;
            case "secure_payload":
                HandleSecurePayload(root);
                break;
            case "room_closed":
                RoomClosed?.Invoke(root.GetProperty("detail").GetString() ?? "Room closed.");
                CurrentSession = null;
                break;
            case "pong":
                break;
        }
    }

    private void HandleSecurePayload(JsonElement root)
    {
        var channel = root.GetProperty("channel").GetString() ?? string.Empty;
        var nonceBase64 = root.GetProperty("nonce_base64").GetString() ?? string.Empty;
        var ciphertextBase64 = root.GetProperty("ciphertext_base64").GetString() ?? string.Empty;

        using var payload = _roomSecurity.DecryptToDocument(channel, nonceBase64, ciphertextBase64);
        var secureRoot = payload.RootElement;

        switch (channel)
        {
            case "frame":
                FrameReceived?.Invoke(
                    new RemoteFrame(
                        Convert.FromBase64String(ReadString(secureRoot, "jpeg_base64")),
                        ReadInt(secureRoot, "width"),
                        ReadInt(secureRoot, "height")));
                break;
            case "chat":
                ChatReceived?.Invoke(
                    new ChatMessage(
                        ReadString(secureRoot, "sender"),
                        ReadString(secureRoot, "role"),
                        ReadString(secureRoot, "text")));
                break;
            case "input":
                InputReceived?.Invoke(
                    new RemoteInputCommand(
                        EventName: ReadString(secureRoot, "event"),
                        XRatio: ReadNullableDouble(secureRoot, "x_ratio"),
                        YRatio: ReadNullableDouble(secureRoot, "y_ratio"),
                        Button: ReadNullableString(secureRoot, "button"),
                        Delta: ReadNullableInt(secureRoot, "delta"),
                        KeyCode: ReadNullableInt(secureRoot, "key_code")));
                break;
            case "file_offer":
                FileOfferReceived?.Invoke(ParseFileOffer(secureRoot));
                break;
            case "file_chunk":
                FileChunkReceived?.Invoke(ParseFileChunk(secureRoot));
                break;
            case "file_complete":
                FileTransferCompleted?.Invoke(ParseFileComplete(secureRoot));
                break;
        }
    }

    private static IReadOnlyList<ParticipantInfo> ParseParticipants(JsonElement root)
    {
        var participants = new List<ParticipantInfo>();
        foreach (var participant in root.GetProperty("participants").EnumerateArray())
        {
            participants.Add(
                new ParticipantInfo(
                    participant.GetProperty("client_id").GetString() ?? string.Empty,
                    participant.GetProperty("display_name").GetString() ?? string.Empty,
                    ParseRole(participant.GetProperty("role").GetString()),
                    ParseMode(participant.GetProperty("access_mode").GetString()),
                    participant.GetProperty("is_host").GetBoolean()));
        }

        return participants;
    }

    private static ParticipantRole ParseRole(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "host" => ParticipantRole.Host,
            "controller" => ParticipantRole.Controller,
            "viewer" => ParticipantRole.Viewer,
            _ => ParticipantRole.None,
        };
    }

    private static AppMode ParseMode(string? value)
    {
        return value?.ToLowerInvariant() == "internet" ? AppMode.Internet : AppMode.Lan;
    }

    private static FileTransferOffer ParseFileOffer(JsonElement root)
    {
        return new FileTransferOffer(
            TransferId: ReadString(root, "transfer_id"),
            FileName: ReadString(root, "file_name"),
            FileSize: ReadLong(root, "file_size"),
            TotalChunks: ReadInt(root, "total_chunks"),
            SenderName: ReadString(root, "sender_name"),
            SenderClientId: ReadString(root, "sender_client_id"));
    }

    private static FileTransferChunk ParseFileChunk(JsonElement root)
    {
        return new FileTransferChunk(
            TransferId: ReadString(root, "transfer_id"),
            ChunkIndex: ReadInt(root, "chunk_index"),
            TotalChunks: ReadInt(root, "total_chunks"),
            ChunkBytes: Convert.FromBase64String(ReadString(root, "chunk_base64")));
    }

    private static FileTransferComplete ParseFileComplete(JsonElement root)
    {
        return new FileTransferComplete(ReadString(root, "transfer_id"));
    }

    private static string ReadString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string? ReadNullableString(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static int ReadInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : 0;
    }

    private static long ReadLong(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt64()
            : 0L;
    }

    private static int? ReadNullableInt(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : null;
    }

    private static double? ReadNullableDouble(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetDouble()
            : null;
    }

    public void Dispose()
    {
        _sendLock.Dispose();
        _roomSecurity.Dispose();
        _connectionCts?.Dispose();
        _socket?.Dispose();
    }
}
