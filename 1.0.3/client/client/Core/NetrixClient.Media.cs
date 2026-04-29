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
                ["jpeg_base64"] = Convert.ToBase64String(frame.JpegBytes),
                ["width"] = frame.Width,
                ["height"] = frame.Height,
                ["sent_at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            cancellationToken);
    }

    private Task SendSecureFrameAsync(RemoteFrame frame, CancellationToken cancellationToken)
    {
        var plaintext = new byte[16 + frame.JpegBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(0, 4), frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(4, 4), frame.Height);
        BinaryPrimitives.WriteInt64LittleEndian(plaintext.AsSpan(8, 8), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Buffer.BlockCopy(frame.JpegBytes, 0, plaintext, 16, frame.JpegBytes.Length);

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
