using System.Buffers.Binary;

namespace client.Core;

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
        var payload = frame.PayloadBytes;
        var plaintext = new byte[20 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(0, 4), (int)frame.Codec);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(4, 4), frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(8, 4), frame.Height);
        BinaryPrimitives.WriteInt64LittleEndian(plaintext.AsSpan(12, 8), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Buffer.BlockCopy(payload, 0, plaintext, 20, payload.Length);

        var encrypted = _roomSecurity.EncryptBytes("frame", plaintext);
        var packet = new byte[SecureFrameMagic.Length + encrypted.Nonce.Length + encrypted.CiphertextWithTag.Length];
        Buffer.BlockCopy(SecureFrameMagic, 0, packet, 0, SecureFrameMagic.Length);
        Buffer.BlockCopy(encrypted.Nonce, 0, packet, SecureFrameMagic.Length, encrypted.Nonce.Length);
        Buffer.BlockCopy(
            encrypted.CiphertextWithTag,
            0,
            packet,
            SecureFrameMagic.Length + encrypted.Nonce.Length,
            encrypted.CiphertextWithTag.Length);

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
}
