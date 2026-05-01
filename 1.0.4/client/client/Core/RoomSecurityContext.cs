using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace client.Core;

internal sealed class RoomSecurityContext : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private byte[]? _key;

    public bool IsEnabled => _key is { Length: > 0 };

    public void Configure(string roomId, string roomPassword, bool enabled)
    {
        Clear();

        if (!enabled)
        {
            return;
        }

        var salt = Encoding.UTF8.GetBytes($"netrix-room:{roomId}:v1");
        _key = Rfc2898DeriveBytes.Pbkdf2(
            roomPassword,
            salt,
            120_000,
            HashAlgorithmName.SHA256,
            32);
    }

    public (string NonceBase64, string CiphertextBase64) EncryptJson(string channel, string json)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("Room security is not configured.");
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(json);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(channel));

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return (Convert.ToBase64String(nonce), Convert.ToBase64String(combined));
    }

    public (byte[] Nonce, byte[] CiphertextWithTag) EncryptBytes(string channel, byte[] plaintext)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("Room security is not configured.");
        }

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(channel));

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return (nonce, combined);
    }

    public JsonDocument DecryptToDocument(string channel, string nonceBase64, string ciphertextBase64)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("Room security is not configured.");
        }

        var nonce = Convert.FromBase64String(nonceBase64);
        var combined = Convert.FromBase64String(ciphertextBase64);
        if (combined.Length < TagSize)
        {
            throw new InvalidOperationException("Encrypted payload is invalid.");
        }

        var ciphertext = combined[..^TagSize];
        var tag = combined[^TagSize..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(channel));
        return JsonDocument.Parse(plaintext);
    }

    public byte[] DecryptBytes(string channel, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertextWithTag)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("Room security is not configured.");
        }

        if (ciphertextWithTag.Length < TagSize)
        {
            throw new InvalidOperationException("Encrypted payload is invalid.");
        }

        var ciphertext = ciphertextWithTag[..^TagSize];
        var tag = ciphertextWithTag[^TagSize..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(channel));
        return plaintext;
    }

    public void Clear()
    {
        if (_key is null)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_key);
        _key = null;
    }

    public void Dispose()
    {
        Clear();
    }
}
