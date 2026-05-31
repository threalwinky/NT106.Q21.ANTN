using System.Net.Sockets;

using client.Cryptography;
using client.Models;

namespace client.Networking;

internal sealed partial class NetrixClient : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly RoomSecurityContext _roomSecurity = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveLoop;
    private string? _connectedUrl;
    private string? _currentDisplayName;
    private string? _lastDisconnectDetail;
    private string? _pendingRoomPassword;

    public event Action<string>? StatusChanged;
    public event Action<string>? ErrorReceived;
    public event Action<RoomSessionInfo>? RoomReady;
    public event Action<IReadOnlyList<ParticipantInfo>>? ParticipantsUpdated;
    public event Action<RemoteFrame>? FrameReceived;
    public event Action<ChatMessage>? ChatReceived;
    public event Action<ProcessRequestInfo>? ProcessRequestReceived;
    public event Action<ProcessSnapshotInfo>? ProcessSnapshotReceived;
    public event Action<RemoteInputCommand>? InputReceived;
    public event Action<FileTransferOffer>? FileOfferReceived;
    public event Action<FileTransferChunk>? FileChunkReceived;
    public event Action<FileTransferComplete>? FileTransferCompleted;
    public event Action<string>? ClipboardReceived;
    public event Action<string>? RoomClosed;
    public event Action<ControlRequestInfo>? ControlRequestReceived;
    public event Action<ControllerPermissionInfo>? ControllerPermissionChanged;

    public bool IsConnected => _client?.Connected == true && _stream is not null;
    public bool HasHealthyConnection => IsConnected && _receiveLoop is { IsCompleted: false };

    public RoomSessionInfo? CurrentSession { get; private set; }

    public void Dispose()
    {
        _sendLock.Dispose();
        _connectionLock.Dispose();
        _roomSecurity.Dispose();
        _connectionCts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
    }
}
