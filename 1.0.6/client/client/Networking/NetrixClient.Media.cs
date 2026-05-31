using client.Models;

namespace client.Networking;

internal sealed partial class NetrixClient
{
    public Task SendFrameAsync(RemoteFrame frame, CancellationToken cancellationToken)
    {
        if (_roomSecurity.IsEnabled)
        {
            return SendSecureFrameAsync(frame, cancellationToken);
        }

        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "frame",
                ["codec"] = FormatFrameCodec(frame.Codec),
                ["payload_base64"] = Convert.ToBase64String(frame.PayloadBytes),
                ["jpeg_base64"] = frame.Codec == RemoteFrameCodec.Jpeg
                    ? Convert.ToBase64String(frame.PayloadBytes)
                    : string.Empty,
                ["width"] = frame.Width,
                ["height"] = frame.Height,
                ["sent_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            cancellationToken);
    }

    private Task SendSecureFrameAsync(RemoteFrame frame, CancellationToken cancellationToken)
    {
        var packet = SecureFramePacketCodec.Build(_roomSecurity, frame);
        return SendBinaryAsync(packet, cancellationToken);
    }

    private static string FormatFrameCodec(RemoteFrameCodec codec)
    {
        return codec switch
        {
            RemoteFrameCodec.H264 => "h264",
            _ => "jpeg",
        };
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

    public Task SendClipboardAsync(string text, CancellationToken cancellationToken)
    {
        return SendSecurePayloadAsync(
            "clipboard",
            new
            {
                text,
            },
            cancellationToken);
    }
}
