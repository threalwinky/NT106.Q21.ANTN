using System.Buffers.Binary;

using client.Cryptography;
using client.Models;

namespace client.Networking;

internal static class SecureFramePacketCodec
{
    public static readonly byte[] Magic = [(byte)'N', (byte)'X', (byte)'F', (byte)'1'];
    public const int NonceLength = 12;

    public static byte[] Build(RoomSecurityContext roomSecurity, RemoteFrame frame)
    {
        var payload = frame.PayloadBytes;
        var plaintext = new byte[20 + payload.Length];
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(0, 4), (int)frame.Codec);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(4, 4), frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(plaintext.AsSpan(8, 4), frame.Height);
        BinaryPrimitives.WriteInt64LittleEndian(plaintext.AsSpan(12, 8), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        Buffer.BlockCopy(payload, 0, plaintext, 20, payload.Length);

        var encrypted = roomSecurity.EncryptBytes("frame", plaintext);
        var packet = new byte[Magic.Length + encrypted.Nonce.Length + encrypted.CiphertextWithTag.Length];
        Buffer.BlockCopy(Magic, 0, packet, 0, Magic.Length);
        Buffer.BlockCopy(encrypted.Nonce, 0, packet, Magic.Length, encrypted.Nonce.Length);
        Buffer.BlockCopy(encrypted.CiphertextWithTag, 0, packet, Magic.Length + encrypted.Nonce.Length, encrypted.CiphertextWithTag.Length);
        return packet;
    }

    public static RemoteFrame Decode(RoomSecurityContext roomSecurity, byte[] packet)
    {
        if (packet.Length <= Magic.Length + NonceLength)
        {
            throw new InvalidOperationException("Received an incomplete binary frame.");
        }

        if (!packet.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidOperationException("Received an unsupported binary message.");
        }

        var nonce = packet.AsSpan(Magic.Length, NonceLength);
        var ciphertextWithTag = packet.AsSpan(Magic.Length + NonceLength);
        var plaintext = roomSecurity.DecryptBytes("frame", nonce, ciphertextWithTag);
        if (plaintext.Length < 16)
        {
            throw new InvalidOperationException("Received an invalid binary frame payload.");
        }

        var firstValue = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(0, 4));
        RemoteFrameCodec codec;
        int width;
        int height;
        int payloadOffset;

        if (plaintext.Length >= 20 && TryParseFrameCodec(firstValue, out codec))
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(4, 4));
            height = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(8, 4));
            payloadOffset = 20;
        }
        else
        {
            codec = RemoteFrameCodec.Jpeg;
            width = firstValue;
            height = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(4, 4));
            payloadOffset = 16;
        }

        return new RemoteFrame(plaintext.AsSpan(payloadOffset).ToArray(), width, height, codec);
    }

    private static bool TryParseFrameCodec(int value, out RemoteFrameCodec codec)
    {
        codec = value switch
        {
            (int)RemoteFrameCodec.Jpeg => RemoteFrameCodec.Jpeg,
            (int)RemoteFrameCodec.H264 => RemoteFrameCodec.H264,
            _ => default,
        };

        return codec != default;
    }
}
