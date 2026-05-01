using client.Core;

namespace client;

partial class MainForm
{
    private void UpdateModeUi()
    {
        ApplyCaptureBackendPreference();
        if (_lanMainServerField is not null)
        {
            _lanMainServerField.Visible = CurrentMode == AppMode.Lan;
            _lanMainServerField.Parent?.PerformLayout();
        }

        _authStatusLabel.Text = IsAuthenticated
            ? $"Signed in as {_authenticatedUsername}"
            : "Sign in required";
        RefreshInteractiveStyles();
        RefreshSessionChrome();
    }

    private void UpdateStatus(string message)
    {
        _statusLabel.Text = message;
    }

    private void SetSessionActionBusy(bool busy, string? message = null)
    {
        _isSessionActionInProgress = busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            UpdateStatus(message);
        }

        RefreshSessionChrome();
    }

    private void SetButtonInteractive(Button button, bool interactive)
    {
        _buttonInteractivity[button] = interactive;
        button.Enabled = true;
        button.TabStop = interactive;
    }

    private bool IsButtonInteractive(Button button)
    {
        return !_buttonInteractivity.TryGetValue(button, out var interactive) || interactive;
    }

    private void SetTextInputInteractive(TextBox textBox, bool interactive)
    {
        _textInputInteractivity[textBox] = interactive;
        textBox.Enabled = true;
        textBox.ReadOnly = !interactive;
        textBox.TabStop = interactive;
        textBox.ShortcutsEnabled = interactive;
    }

    private bool IsTextInputInteractive(TextBox textBox)
    {
        return !_textInputInteractivity.TryGetValue(textBox, out var interactive) || interactive;
    }

    private void ShowErrorDialog(string message, string title = "Netrix Error")
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        OnUiThread(() => MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error));
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

    private void ValidateLoginCredentials()
    {
        ValidateAuthCredentials();
    }

    private void ValidateRegisterCredentials()
    {
        if (_registerUsernameTextBox.Text.Trim().Length < 3)
        {
            throw new InvalidOperationException("Username must be at least 3 characters.");
        }

        if (_registerPasswordTextBox.Text.Length < 4)
        {
            throw new InvalidOperationException("Password must be at least 4 characters.");
        }

        if (!string.Equals(_registerPasswordTextBox.Text, _registerConfirmPasswordTextBox.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Password confirmation does not match.");
        }
    }

    private void EnsureAuthenticated()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(_accessToken))
        {
            throw new InvalidOperationException("Sign in before creating or joining a room.");
        }
    }

    private string ResolveDisplayName()
    {
        var displayName = _displayNameTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!string.IsNullOrWhiteSpace(_authenticatedUsername))
        {
            return _authenticatedUsername;
        }

        return Environment.UserName;
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
