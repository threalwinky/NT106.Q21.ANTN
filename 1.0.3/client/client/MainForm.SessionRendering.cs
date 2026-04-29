using System.Drawing;
using System.IO;
using client.Core;

namespace client;

partial class MainForm
{
    private void HandleRoomReady(RoomSessionInfo session)
    {
        CompletePendingRoomRequest(session);
        _roomIdTextBox.Text = session.RoomId;
        _roomStatusLabel.Text = $"Room {session.RoomId} | Role {FormatRole(session.Role)}";
        _chatListBox.Items.Clear();
        _remoteInputActive = session.Role == ParticipantRole.Controller && session.CanSendControl;
        SyncRoomSecurityState();
        ShowControlView();

        if (session.Role == ParticipantRole.Host)
        {
            StartHostStreaming();
            UpdateStatus($"Hosting room. Share room hash {session.RoomId} and the room password with approved peers.");
        }
        else if (session.Role == ParticipantRole.Controller && !session.CanSendControl)
        {
            StopHostStreaming();
            UpdateStatus("Joined as controller. Waiting for host approval before control is enabled.");
        }
        else
        {
            StopHostStreaming();
            UpdateStatus("Joined room successfully.");
        }

        RefreshSessionChrome();
    }

    private void RenderParticipants(IReadOnlyList<ParticipantInfo> participants)
    {
        _participantsListBox.Items.Clear();
        foreach (var participant in participants)
        {
            var suffix = participant.IsHost
                ? " | Host"
                : participant.Role == ParticipantRole.Controller && !participant.CanSendControl
                    ? " | Pending approval"
                    : string.Empty;
            _participantsListBox.Items.Add($"{participant.DisplayName} [{FormatRole(participant.Role)}]{suffix}");
        }

        _remoteInputActive = _netrixClient.CurrentSession?.Role == ParticipantRole.Controller
            && _netrixClient.CurrentSession?.CanSendControl == true;
        RefreshSessionChrome();
    }

    private void RenderFrame(RemoteFrame frame)
    {
        using var stream = new MemoryStream(frame.JpegBytes);
        using var image = Image.FromStream(stream);
        var clonedImage = new Bitmap(image);

        var oldImage = _remoteScreenBox.Image;
        _remoteScreenBox.Image = clonedImage;
        oldImage?.Dispose();
        RefreshSessionChrome();
    }
}
