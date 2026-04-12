using System.Text.Json;

namespace client.Core;

internal sealed partial class NetrixClient
{
    public void ConfigureRoomSecurity(string roomId, string roomPassword, bool enabled)
    {
        _roomSecurity.Configure(roomId, roomPassword, enabled);
    }

    public void ClearRoomSecurity()
    {
        _roomSecurity.Clear();
    }

    private Task SendSecurePayloadAsync(string channel, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        var encrypted = _roomSecurity.EncryptJson(channel, json);

        return SendAsync(
            new Dictionary<string, object?>
            {
                ["type"] = "secure_payload",
                ["channel"] = channel,
                ["nonce_base64"] = encrypted.NonceBase64,
                ["ciphertext_base64"] = encrypted.CiphertextBase64,
            },
            cancellationToken);
    }
}
