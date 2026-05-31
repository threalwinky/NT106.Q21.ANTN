using System.Text.Json;

using client.Models;

namespace client.Networking;

internal sealed partial class NetrixClient
{
    public void ConfigureRoomSecurity(string roomId, string roomPassword, bool enabled)
    {
        _roomSecurity.Configure(roomId, roomPassword, enabled);
    }

    public void ClearRoomSecurity()
    {
        _pendingRoomPassword = null;
        _roomSecurity.Clear();
    }

    private void ConfigureRoomSecurityFromPendingPassword(string roomId)
    {
        var roomPassword = _pendingRoomPassword;
        _pendingRoomPassword = null;

        if (string.IsNullOrWhiteSpace(roomPassword))
        {
            _roomSecurity.Clear();
            return;
        }

        _roomSecurity.Configure(roomId, roomPassword, enabled: true);
    }

    private Task SendSecurePayloadAsync(
        string channel,
        object payload,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var json = JsonSerializer.Serialize(payload);
        var encrypted = _roomSecurity.EncryptJson(channel, json);

        var message = new Dictionary<string, object?>
        {
            ["type"] = "secure_payload",
            ["channel"] = channel,
            ["nonce_base64"] = encrypted.NonceBase64,
            ["ciphertext_base64"] = encrypted.CiphertextBase64,
        };

        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                message[item.Key] = item.Value;
            }
        }

        return SendAsync(
            message,
            cancellationToken);
    }
}
