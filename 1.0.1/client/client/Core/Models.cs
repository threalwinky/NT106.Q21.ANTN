namespace client.Core;

internal sealed record RoomSessionInfo(
    string RoomId,
    string ClientId,
    ParticipantRole Role,
    AppMode Mode,
    string? HostClientId = null);

internal sealed record ParticipantInfo(
    string ClientId,
    string DisplayName,
    ParticipantRole Role,
    AppMode Mode,
    bool IsHost);

internal sealed record ChatMessage(string Sender, string Role, string Text);

internal sealed record RemoteFrame(byte[] JpegBytes, int Width, int Height);

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
