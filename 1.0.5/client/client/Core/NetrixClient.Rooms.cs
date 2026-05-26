namespace client.Core;

internal sealed partial class NetrixClient
{
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

    public Task PingAsync(CancellationToken cancellationToken)
    {
        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "ping",
            },
            cancellationToken);
    }

    public Task SendControlDecisionAsync(string targetClientId, bool approved, CancellationToken cancellationToken)
    {
        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "control_decision",
                ["target_client_id"] = targetClientId,
                ["approved"] = approved,
            },
            cancellationToken);
    }
}
