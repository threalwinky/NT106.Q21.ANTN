using System.Net.WebSockets;

namespace client.Core;

internal sealed partial class NetrixClient : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
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
    public event Action<ControlRequestInfo>? ControlRequestReceived;
    public event Action<ControllerPermissionInfo>? ControllerPermissionChanged;

    public bool IsConnected => _socket?.State == WebSocketState.Open;
    public bool HasHealthyConnection => _socket?.State == WebSocketState.Open && _receiveLoop is { IsCompleted: false };

    public RoomSessionInfo? CurrentSession { get; private set; }

    public void Dispose()
    {
        _sendLock.Dispose();
        _connectionLock.Dispose();
        _roomSecurity.Dispose();
        _connectionCts?.Dispose();
        _socket?.Dispose();
    }
}
