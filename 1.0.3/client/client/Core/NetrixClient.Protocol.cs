using System.Buffers.Binary;
using System.Text.Json;

namespace client.Core;

internal sealed partial class NetrixClient
{
    private void HandleBinaryServerMessage(byte[] packet)
    {
        if (packet.Length <= SecureFrameMagic.Length + SecureFrameNonceLength)
        {
            throw new InvalidOperationException("Received an incomplete binary frame.");
        }

        if (!packet.AsSpan(0, SecureFrameMagic.Length).SequenceEqual(SecureFrameMagic))
        {
            throw new InvalidOperationException("Received an unsupported binary message.");
        }

        var nonce = packet.AsSpan(SecureFrameMagic.Length, SecureFrameNonceLength);
        var ciphertextWithTag = packet.AsSpan(SecureFrameMagic.Length + SecureFrameNonceLength);
        var plaintext = _roomSecurity.DecryptBytes("frame", nonce, ciphertextWithTag);
        if (plaintext.Length < 16)
        {
            throw new InvalidOperationException("Received an invalid binary frame payload.");
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(0, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(plaintext.AsSpan(4, 4));
        var jpegBytes = plaintext.AsSpan(16).ToArray();
        FrameReceived?.Invoke(new RemoteFrame(jpegBytes, width, height));
    }

    private void HandleServerMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var messageType = root.GetProperty("type").GetString() ?? string.Empty;

        switch (messageType)
        {
            case "hello":
                StatusChanged?.Invoke(root.GetProperty("message").GetString() ?? "Connected");
                break;
            case "error":
                ErrorReceived?.Invoke(root.GetProperty("detail").GetString() ?? "Unknown error");
                break;
            case "room_created":
                CurrentSession = new RoomSessionInfo(
                    root.GetProperty("room_id").GetString() ?? string.Empty,
                    root.GetProperty("client_id").GetString() ?? string.Empty,
                    ParseRole(root.GetProperty("role").GetString()),
                    ParseMode(root.GetProperty("access_mode").GetString()),
                    CanSendControl: root.TryGetProperty("control_approved", out var createdApproved) ? createdApproved.GetBoolean() : true);
                RoomReady?.Invoke(CurrentSession);
                break;
            case "joined_room":
                CurrentSession = new RoomSessionInfo(
                    root.GetProperty("room_id").GetString() ?? string.Empty,
                    root.GetProperty("client_id").GetString() ?? string.Empty,
                    ParseRole(root.GetProperty("role").GetString()),
                    ParseMode(root.GetProperty("access_mode").GetString()),
                    root.TryGetProperty("host_client_id", out var hostClientId) ? hostClientId.GetString() : null,
                    root.TryGetProperty("control_approved", out var controlApproved) ? controlApproved.GetBoolean() : true);
                RoomReady?.Invoke(CurrentSession);
                break;
            case "room_state":
                var participants = ParseParticipants(root);
                if (CurrentSession is not null)
                {
                    var currentParticipant = participants.FirstOrDefault(participant => participant.ClientId == CurrentSession.ClientId);
                    if (currentParticipant is not null)
                    {
                        CurrentSession = CurrentSession with
                        {
                            Role = currentParticipant.Role,
                            Mode = currentParticipant.Mode,
                            CanSendControl = currentParticipant.CanSendControl,
                        };
                    }
                }
                ParticipantsUpdated?.Invoke(participants);
                break;
            case "frame":
                var bytes = Convert.FromBase64String(root.GetProperty("jpeg_base64").GetString() ?? string.Empty);
                FrameReceived?.Invoke(new RemoteFrame(bytes, root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32()));
                break;
            case "chat":
                ChatReceived?.Invoke(
                    new ChatMessage(
                        root.GetProperty("sender").GetString() ?? "Unknown",
                        root.GetProperty("role").GetString() ?? string.Empty,
                        root.GetProperty("text").GetString() ?? string.Empty));
                break;
            case "file_offer":
                FileOfferReceived?.Invoke(ParseFileOffer(root));
                break;
            case "file_chunk":
                FileChunkReceived?.Invoke(ParseFileChunk(root));
                break;
            case "file_complete":
                FileTransferCompleted?.Invoke(ParseFileComplete(root));
                break;
            case "input":
                InputReceived?.Invoke(
                    new RemoteInputCommand(
                        root.GetProperty("event").GetString() ?? string.Empty,
                        root.TryGetProperty("x_ratio", out var xRatio) && xRatio.ValueKind != JsonValueKind.Null ? xRatio.GetDouble() : null,
                        root.TryGetProperty("y_ratio", out var yRatio) && yRatio.ValueKind != JsonValueKind.Null ? yRatio.GetDouble() : null,
                        root.TryGetProperty("button", out var button) ? button.GetString() : null,
                        root.TryGetProperty("delta", out var delta) && delta.ValueKind != JsonValueKind.Null ? delta.GetInt32() : null,
                        root.TryGetProperty("key_code", out var keyCode) && keyCode.ValueKind != JsonValueKind.Null ? keyCode.GetInt32() : null));
                break;
            case "secure_payload":
                HandleSecurePayload(root);
                break;
            case "room_closed":
                RoomClosed?.Invoke(root.GetProperty("detail").GetString() ?? "Room closed.");
                CurrentSession = null;
                break;
            case "control_request":
                ControlRequestReceived?.Invoke(
                    new ControlRequestInfo(
                        root.GetProperty("room_id").GetString() ?? string.Empty,
                        root.GetProperty("target_client_id").GetString() ?? string.Empty,
                        root.GetProperty("display_name").GetString() ?? "Unknown"));
                break;
            case "control_granted":
                if (CurrentSession is not null && root.GetProperty("target_client_id").GetString() == CurrentSession.ClientId)
                {
                    CurrentSession = CurrentSession with { CanSendControl = true };
                }
                ControllerPermissionChanged?.Invoke(
                    new ControllerPermissionInfo(
                        root.GetProperty("room_id").GetString() ?? string.Empty,
                        root.GetProperty("target_client_id").GetString() ?? string.Empty,
                        true,
                        root.GetProperty("detail").GetString() ?? "Host approved remote control."));
                break;
            case "control_denied":
                if (CurrentSession is not null && root.GetProperty("target_client_id").GetString() == CurrentSession.ClientId)
                {
                    CurrentSession = CurrentSession with { Role = ParticipantRole.Viewer, CanSendControl = false };
                }
                ControllerPermissionChanged?.Invoke(
                    new ControllerPermissionInfo(
                        root.GetProperty("room_id").GetString() ?? string.Empty,
                        root.GetProperty("target_client_id").GetString() ?? string.Empty,
                        false,
                        root.GetProperty("detail").GetString() ?? "Host denied remote control."));
                break;
            case "pong":
                break;
        }
    }

    private void HandleSecurePayload(JsonElement root)
    {
        var channel = root.GetProperty("channel").GetString() ?? string.Empty;
        var nonceBase64 = root.GetProperty("nonce_base64").GetString() ?? string.Empty;
        var ciphertextBase64 = root.GetProperty("ciphertext_base64").GetString() ?? string.Empty;

        using var payload = _roomSecurity.DecryptToDocument(channel, nonceBase64, ciphertextBase64);
        var secureRoot = payload.RootElement;

        switch (channel)
        {
            case "frame":
                FrameReceived?.Invoke(
                    new RemoteFrame(
                        Convert.FromBase64String(ReadString(secureRoot, "jpeg_base64")),
                        ReadInt(secureRoot, "width"),
                        ReadInt(secureRoot, "height")));
                break;
            case "chat":
                ChatReceived?.Invoke(
                    new ChatMessage(
                        ReadString(secureRoot, "sender"),
                        ReadString(secureRoot, "role"),
                        ReadString(secureRoot, "text")));
                break;
            case "input":
                InputReceived?.Invoke(
                    new RemoteInputCommand(
                        EventName: ReadString(secureRoot, "event"),
                        XRatio: ReadNullableDouble(secureRoot, "x_ratio"),
                        YRatio: ReadNullableDouble(secureRoot, "y_ratio"),
                        Button: ReadNullableString(secureRoot, "button"),
                        Delta: ReadNullableInt(secureRoot, "delta"),
                        KeyCode: ReadNullableInt(secureRoot, "key_code")));
                break;
            case "file_offer":
                FileOfferReceived?.Invoke(ParseFileOffer(secureRoot));
                break;
            case "file_chunk":
                FileChunkReceived?.Invoke(ParseFileChunk(secureRoot));
                break;
            case "file_complete":
                FileTransferCompleted?.Invoke(ParseFileComplete(secureRoot));
                break;
        }
    }
}
