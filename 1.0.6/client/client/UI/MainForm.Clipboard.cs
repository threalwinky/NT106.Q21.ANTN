using System.Runtime.InteropServices;

namespace client.UI;

partial class MainForm
{
    private const int WmClipboardUpdate = 0x031D;
    private const int MaxClipboardTextChars = 256 * 1024;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_clipboardListenerRegistered)
        {
            _clipboardListenerRegistered = AddClipboardFormatListener(Handle);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_clipboardListenerRegistered)
        {
            RemoveClipboardFormatListener(Handle);
            _clipboardListenerRegistered = false;
        }

        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmClipboardUpdate)
        {
            BroadcastClipboardIfNeeded();
        }
    }

    private void BroadcastClipboardIfNeeded()
    {
        if (!_netrixClient.IsConnected || _netrixClient.CurrentSession is null)
        {
            return;
        }

        if (DateTime.UtcNow < _clipboardSuppressUntilUtc)
        {
            return;
        }

        try
        {
            if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                return;
            }

            var text = Clipboard.GetText(TextDataFormat.UnicodeText);
            if (string.IsNullOrEmpty(text) || text.Length > MaxClipboardTextChars)
            {
                return;
            }

            if (string.Equals(text, _lastClipboardText, StringComparison.Ordinal))
            {
                return;
            }

            _lastClipboardText = text;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = CreateShortTimeout();
                    await _netrixClient.SendClipboardAsync(text, cts.Token);
                }
                catch (Exception ex)
                {
                    OnUiThread(() => AppendTransferStatus($"Clipboard sync failed: {ex.Message}"));
                }
            });
        }
        catch
        {
        }
    }

    private void ApplyRemoteClipboardText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > MaxClipboardTextChars)
        {
            return;
        }

        try
        {
            if (Clipboard.ContainsText(TextDataFormat.UnicodeText)
                && string.Equals(Clipboard.GetText(TextDataFormat.UnicodeText), text, StringComparison.Ordinal))
            {
                _lastClipboardText = text;
                return;
            }

            _clipboardSuppressUntilUtc = DateTime.UtcNow.AddMilliseconds(500);
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            _lastClipboardText = text;
        }
        catch (Exception ex)
        {
            AppendTransferStatus($"Clipboard apply failed: {ex.Message}");
        }
    }
}
