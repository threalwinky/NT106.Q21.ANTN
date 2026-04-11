using System.Drawing;
using client.Core;

namespace client;

partial class Form1
{
    private async Task SendMouseMoveAsync(MouseEventArgs e)
    {
        if (!CanSendRemoteInput())
        {
            return;
        }

        if (DateTime.UtcNow - _lastMouseMoveSentAtUtc < TimeSpan.FromMilliseconds(40))
        {
            return;
        }

        if (!TryGetRelativePoint(e.Location, out var xRatio, out var yRatio))
        {
            return;
        }

        _lastMouseMoveSentAtUtc = DateTime.UtcNow;
        await SendRemoteInputAsync(new RemoteInputCommand("mouse_move", xRatio, yRatio));
    }

    private async Task SendMouseButtonAsync(string eventName, MouseEventArgs e)
    {
        if (!CanSendRemoteInput() || !TryGetRelativePoint(e.Location, out var xRatio, out var yRatio))
        {
            return;
        }

        var button = e.Button == MouseButtons.Right ? "right" : "left";
        await SendRemoteInputAsync(new RemoteInputCommand(eventName, xRatio, yRatio, button));
    }

    private async Task SendMouseWheelAsync(MouseEventArgs e)
    {
        if (!CanSendRemoteInput())
        {
            return;
        }

        await SendRemoteInputAsync(new RemoteInputCommand("mouse_wheel", Delta: e.Delta));
    }

    private async Task SendKeyAsync(string eventName, KeyEventArgs e)
    {
        if (!CanSendRemoteInput())
        {
            return;
        }

        if (!_remoteScreenBox.Focused)
        {
            return;
        }

        e.SuppressKeyPress = true;
        await SendRemoteInputAsync(new RemoteInputCommand(eventName, KeyCode: (int)e.KeyCode));
    }

    private async Task SendRemoteInputAsync(RemoteInputCommand command)
    {
        try
        {
            using var cts = CreateShortTimeout();
            await _netrixClient.SendInputAsync(command, cts.Token);
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
        }
    }

    private void StartHostStreaming()
    {
        ApplyCaptureBackendPreference();
        StopHostStreaming();
        _hostCaptureCts = new CancellationTokenSource();
        _ = Task.Run(() => HostStreamingLoopAsync(_hostCaptureCts.Token));
    }

    private async Task HostStreamingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var session = _netrixClient.CurrentSession;
                if (!_netrixClient.IsConnected || session?.Role != ParticipantRole.Host)
                {
                    return;
                }

                var frame = await Task.Run(_screenCaptureService.CapturePrimaryScreen, cancellationToken);
                if (frame is null)
                {
                    await Task.Delay(20, cancellationToken);
                    continue;
                }

                await _netrixClient.SendFrameAsync(frame, cancellationToken);
                await Task.Delay(90, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                OnUiThread(() => UpdateStatus($"Streaming stopped: {ex.Message}"));
                return;
            }
        }
    }

    private void StopHostStreaming()
    {
        if (_hostCaptureCts is null)
        {
            return;
        }

        _hostCaptureCts.Cancel();
        _hostCaptureCts.Dispose();
        _hostCaptureCts = null;
    }

    private void StartPingLoop()
    {
        StopPingLoop();
        _pingCts = new CancellationTokenSource();
        _ = Task.Run(() => PingLoopAsync(_pingCts.Token));
    }

    private async Task PingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                if (_netrixClient.IsConnected)
                {
                    await _netrixClient.PingAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }
        }
    }

    private void StopPingLoop()
    {
        if (_pingCts is null)
        {
            return;
        }

        _pingCts.Cancel();
        _pingCts.Dispose();
        _pingCts = null;
    }

    private bool CanSendRemoteInput()
    {
        return _remoteInputActive
            && _netrixClient.IsConnected
            && _netrixClient.CurrentSession?.Role == ParticipantRole.Controller
            && _remoteScreenBox.Image is not null;
    }

    private bool TryGetRelativePoint(Point mouseLocation, out double xRatio, out double yRatio)
    {
        xRatio = 0;
        yRatio = 0;

        if (_remoteScreenBox.Image is null || _remoteScreenBox.ClientSize.Width == 0 || _remoteScreenBox.ClientSize.Height == 0)
        {
            return false;
        }

        var imageWidth = _remoteScreenBox.Image.Width;
        var imageHeight = _remoteScreenBox.Image.Height;
        var clientWidth = _remoteScreenBox.ClientSize.Width;
        var clientHeight = _remoteScreenBox.ClientSize.Height;

        var imageAspect = imageWidth / (double)imageHeight;
        var boxAspect = clientWidth / (double)clientHeight;

        int renderWidth;
        int renderHeight;
        int offsetX;
        int offsetY;

        if (imageAspect > boxAspect)
        {
            renderWidth = clientWidth;
            renderHeight = (int)(clientWidth / imageAspect);
            offsetX = 0;
            offsetY = (clientHeight - renderHeight) / 2;
        }
        else
        {
            renderHeight = clientHeight;
            renderWidth = (int)(clientHeight * imageAspect);
            offsetX = (clientWidth - renderWidth) / 2;
            offsetY = 0;
        }

        if (mouseLocation.X < offsetX || mouseLocation.X > offsetX + renderWidth || mouseLocation.Y < offsetY || mouseLocation.Y > offsetY + renderHeight)
        {
            return false;
        }

        xRatio = (mouseLocation.X - offsetX) / (double)renderWidth;
        yRatio = (mouseLocation.Y - offsetY) / (double)renderHeight;
        return true;
    }
}
