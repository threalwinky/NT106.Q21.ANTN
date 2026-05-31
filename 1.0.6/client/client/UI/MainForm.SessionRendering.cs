using System.Drawing;
using System.IO;

using client.Models;

namespace client.UI;

partial class MainForm
{
    private void HandleRoomReady(RoomSessionInfo session)
    {
        CompletePendingRoomRequest(session);
        ClearPendingRemoteFrames();
        _roomIdTextBox.Text = session.RoomId;
        _roomStatusLabel.Text = $"Room {session.RoomId} | Role {FormatRole(session.Role)}";
        _chatListBox.Items.Clear();
        _remoteInputActive = session.Role == ParticipantRole.Controller && session.CanSendControl;
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

    private void StartRemoteFrameRenderer()
    {
        if (_remoteFrameRenderThread is not null)
        {
            return;
        }

        _remoteFrameRenderThread = new Thread(RemoteFrameRenderLoop)
        {
            IsBackground = true,
            Name = "Netrix Remote Frame Renderer",
            Priority = ThreadPriority.AboveNormal,
        };
        _remoteFrameRenderThread.Start();
    }

    private void StopRemoteFrameRenderer()
    {
        _remoteFrameRenderCts.Cancel();
        _remoteFrameQueue.CompleteAdding();

        var renderThread = _remoteFrameRenderThread;
        if (renderThread is not null && renderThread.IsAlive && !ReferenceEquals(renderThread, Thread.CurrentThread))
        {
            renderThread.Join(TimeSpan.FromSeconds(1));
        }

        ClearPendingRemoteFrames();
        _remoteFrameRenderCts.Dispose();
    }

    private void QueueRemoteFrame(RemoteFrame frame)
    {
        if (_remoteFrameQueue.IsAddingCompleted)
        {
            return;
        }

        try
        {
            while (!_remoteFrameQueue.TryAdd(frame))
            {
                _remoteFrameQueue.TryTake(out _);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void RemoteFrameRenderLoop()
    {
        try
        {
            foreach (var frame in _remoteFrameQueue.GetConsumingEnumerable(_remoteFrameRenderCts.Token))
            {
                RenderFrameOnWorker(frame);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RenderFrameOnWorker(RemoteFrame frame)
    {
        Bitmap decodedImage;
        try
        {
            decodedImage = frame.Codec switch
            {
                RemoteFrameCodec.H264 => _h264VideoDecoderService.DecodeToBitmap(frame),
                _ => DecodeJpegFrame(frame),
            };
        }
        catch (Exception ex)
        {
            OnUiThread(() => UpdateStatus($"Could not render {frame.Codec} frame: {ex.Message}"));
            return;
        }

        if (IsDisposed || !IsHandleCreated)
        {
            decodedImage.Dispose();
            return;
        }

        OnUiThread(() => PresentDecodedFrame(decodedImage));
    }

    private void PresentDecodedFrame(Bitmap decodedImage)
    {
        if (IsDisposed)
        {
            decodedImage.Dispose();
            return;
        }

        var oldImage = _remoteScreenBox.Image;
        _remoteScreenBox.Image = decodedImage;
        oldImage?.Dispose();
        RefreshSessionChrome();
    }

    private void ClearPendingRemoteFrames()
    {
        while (_remoteFrameQueue.TryTake(out _))
        {
        }
    }

    private static Bitmap DecodeJpegFrame(RemoteFrame frame)
    {
        using var stream = new MemoryStream(frame.PayloadBytes);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }
}
