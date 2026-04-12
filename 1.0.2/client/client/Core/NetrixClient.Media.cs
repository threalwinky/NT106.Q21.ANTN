namespace client.Core;

internal sealed partial class NetrixClient
{
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
}
