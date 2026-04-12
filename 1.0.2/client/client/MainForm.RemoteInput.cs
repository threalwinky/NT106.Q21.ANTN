using System.Drawing;
using client.Core;

namespace client;

partial class MainForm
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

        var key = NormalizeRemoteKey(e.KeyCode);
        if (!key.HasValue)
        {
            return;
        }

        if (string.Equals(eventName, "key_down", StringComparison.Ordinal))
        {
            if (!_pressedRemoteKeys.Add(key.Value))
            {
                return;
            }
        }
        else if (string.Equals(eventName, "key_up", StringComparison.Ordinal))
        {
            _pressedRemoteKeys.Remove(key.Value);
        }

        e.SuppressKeyPress = true;
        await SendRemoteInputAsync(new RemoteInputCommand(eventName, KeyCode: (int)key.Value));
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
            ShowErrorDialog(ex.Message);
        }
    }

    private async Task ReleaseRemoteKeyboardAsync()
    {
        if (_pressedRemoteKeys.Count == 0)
        {
            return;
        }

        var keysToRelease = _pressedRemoteKeys.ToArray();
        _pressedRemoteKeys.Clear();

        foreach (var key in keysToRelease.Reverse())
        {
            try
            {
                using var cts = CreateShortTimeout();
                await _netrixClient.SendInputAsync(new RemoteInputCommand("key_up", KeyCode: (int)key), cts.Token);
            }
            catch
            {
                break;
            }
        }
    }

    private bool CanSendRemoteInput()
    {
        return _remoteInputActive
            && _netrixClient.IsConnected
            && _netrixClient.CurrentSession?.Role == ParticipantRole.Controller
            && _netrixClient.CurrentSession?.CanSendControl == true
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

    private static Keys? NormalizeRemoteKey(Keys key)
    {
        var normalized = key & Keys.KeyCode;
        return normalized switch
        {
            Keys.LWin => null,
            Keys.RWin => null,
            Keys.Apps => null,
            _ => normalized,
        };
    }
}
