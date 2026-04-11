using client.Core;

namespace client;

partial class Form1
{
    private void UpdateModeUi()
    {
        ApplyCaptureBackendPreference();
        var internetMode = CurrentMode == AppMode.Internet;
        _authUrlTextBox.Enabled = internetMode;
        _loadBalancerUrlTextBox.Enabled = internetMode;
        _usernameTextBox.Enabled = internetMode;
        _passwordTextBox.Enabled = internetMode;
        _registerButton.Enabled = true;
        _loginButton.Enabled = true;
        _authStatusLabel.Text = internetMode ? "Authentication required" : "Not needed in LAN mode";
        RefreshInteractiveStyles();
        RefreshSessionChrome();
    }

    private void UpdateStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void ApplyCaptureBackendPreference()
    {
        _screenCaptureService.SetPreference(CaptureBackendPreference.Auto);
    }

    private void ValidateRoomPassword()
    {
        if (_roomPasswordTextBox.Text.Length < 4)
        {
            throw new InvalidOperationException("Room password must be at least 4 characters.");
        }
    }

    private void ValidateAuthCredentials()
    {
        if (_usernameTextBox.Text.Trim().Length < 3)
        {
            throw new InvalidOperationException("Username must be at least 3 characters.");
        }

        if (_passwordTextBox.Text.Length < 4)
        {
            throw new InvalidOperationException("Password must be at least 4 characters.");
        }
    }

    private string ResolveDisplayName()
    {
        var displayName = _displayNameTextBox.Text.Trim();
        return string.IsNullOrWhiteSpace(displayName) ? Environment.UserName : displayName;
    }

    private static CancellationTokenSource CreateShortTimeout()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(20));
    }

    private void OnUiThread(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private string FormatRole(ParticipantRole role)
    {
        return role switch
        {
            ParticipantRole.Host => "Host",
            ParticipantRole.Controller => "Controller",
            ParticipantRole.Viewer => "Viewer",
            _ => "Unknown",
        };
    }
}
