namespace client.Core;

internal sealed record RoomSessionInfo(
    string RoomId,
    string ClientId,
    ParticipantRole Role,
    AppMode Mode,
    string? HostClientId = null,
    bool CanSendControl = true);

internal sealed record ParticipantInfo(
    string ClientId,
    string DisplayName,
    ParticipantRole Role,
    AppMode Mode,
    bool IsHost,
    bool CanSendControl);

internal sealed record ChatMessage(string Sender, string Role, string Text);

internal sealed record ControlRequestInfo(
    string RoomId,
    string TargetClientId,
    string DisplayName);

internal sealed record ControllerPermissionInfo(
    string RoomId,
    string TargetClientId,
    bool Approved,
    string Detail);

internal enum RemoteFrameCodec
{
    Jpeg = 1,
    H264 = 2,
}

internal sealed record RemoteFrame(
    byte[] PayloadBytes,
    int Width,
    int Height,
    RemoteFrameCodec Codec = RemoteFrameCodec.Jpeg)
{
    public byte[] JpegBytes => PayloadBytes;
}

internal sealed record RemoteInputCommand(
    string EventName,
    double? XRatio = null,
    double? YRatio = null,
    string? Button = null,
    int? Delta = null,
    int? KeyCode = null);

internal sealed record FileTransferOffer(
    string TransferId,
    string FileName,
    long FileSize,
    int TotalChunks,
    string SenderName,
    string SenderClientId);

internal sealed record FileTransferChunk(
    string TransferId,
    int ChunkIndex,
    int TotalChunks,
    byte[] ChunkBytes);

internal sealed record FileTransferComplete(string TransferId);

internal sealed record AuthTokenResponse(
    string AccessToken,
    string Username,
    int ExpiresInMinutes);
