using System.Drawing;

namespace client;

partial class Form1
{
    private void ShowControlView()
    {
        _isSettingsViewActive = false;
        UpdateShellStateUi();
    }

    private void ShowSettingsView()
    {
        _isSettingsViewActive = true;
        UpdateShellStateUi();
    }

    private void ToggleSidebar(bool? visible = null)
    {
        _isSidebarVisible = visible ?? !_isSidebarVisible;
        UpdateShellStateUi();
    }

    private void ToggleChatDrawer(bool? visible = null)
    {
        _isChatVisible = visible ?? !_isChatVisible;
        UpdateShellStateUi();
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            _isFullScreen = false;
            FormBorderStyle = _restoreBorderStyle;
            WindowState = FormWindowState.Normal;

            if (_restoreBounds != Rectangle.Empty)
            {
                Bounds = _restoreBounds;
            }

            WindowState = _restoreWindowState;
            _isSidebarVisible = _restoreSidebarVisible;
            _isChatVisible = _restoreChatVisible;
        }
        else
        {
            _restoreBorderStyle = FormBorderStyle;
            _restoreWindowState = WindowState;
            _restoreBounds = Bounds;
            _restoreSidebarVisible = _isSidebarVisible;
            _restoreChatVisible = _isChatVisible;

            _isFullScreen = true;
            _isSidebarVisible = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }

        UpdateShellStateUi();
    }

    private void UpdateShellStateUi()
    {
        _sidebarPanel.Visible = _isSidebarVisible;
        _sidebarSplitter.Visible = _isSidebarVisible;
        _toggleSidebarButton.Visible = _isSidebarVisible;
        _showSidebarButton.Visible = !_isSidebarVisible;
        _controlViewPanel.Visible = !_isSettingsViewActive;
        _settingsViewPanel.Visible = _isSettingsViewActive;
        _chatDrawerPanel.Visible = _isChatVisible;
        _chatSplitter.Visible = _isChatVisible;

        _toggleSidebarButton.Text = "<";
        _showSidebarButton.Text = ">";
        _toggleChatButton.Text = _isChatVisible ? "Hide Chat" : "Show Chat";
        _closeChatDrawerButton.Text = "Hide";
        _toggleFullScreenButton.Text = _isFullScreen ? "Exit Full" : "Full Screen";

        RefreshSessionChrome();
    }

    private void UpdateThemeToggleText()
    {
        _darkThemeCheckBox.Text = IsDarkTheme ? "Dark Theme" : "Light Theme";
    }

    private void RefreshSessionChrome()
    {
        var session = _netrixClient.CurrentSession;
        var hasSession = session is not null;
        var participantCount = _participantsListBox.Items.Count;
        var mode = session?.Mode ?? CurrentMode;
        var modeText = mode == Core.AppMode.Internet ? "Internet" : "LAN";

        _participantsTitleLabel.Text = participantCount == 0 ? "Connected peers" : $"Connected peers ({participantCount})";
        _toggleChatButton.Enabled = true;
        _closeChatDrawerButton.Enabled = true;
        _sendChatButton.Enabled = true;
        _chatInputTextBox.Enabled = hasSession;
        _disconnectButton.Enabled = true;
        _sendFileButton.Enabled = true;
        _transferTitleLabel.Text = _transferListBox.Items.Count == 0
            ? "Transfer activity"
            : $"Transfer activity ({_transferListBox.Items.Count})";
        RefreshInteractiveStyles();

        if (!hasSession)
        {
            _toolbarSessionLabel.Text = $"Remote Session | {modeText}";
            _roomStatusLabel.Text = "No active room";
            _remoteHintLabel.Text = "Create or join a room to start remote viewing and control.";
            return;
        }

        var roleText = FormatRole(session.Role);
        _toolbarSessionLabel.Text = $"Room {session.RoomId} | {roleText} | {modeText}";
        _roomStatusLabel.Text = $"Room {session.RoomId} | Role {roleText}";
        _remoteHintLabel.Text = session.Role switch
        {
            Core.ParticipantRole.Host => "Hosting this room. Remote input is only accepted from connected controllers.",
            Core.ParticipantRole.Controller when _remoteInputActive => "Remote input is active. Use Escape to leave full screen.",
            Core.ParticipantRole.Controller => "Click inside the remote screen before sending mouse or keyboard input.",
            Core.ParticipantRole.Viewer => "Viewer mode is read-only. Rejoin as Controller to take control.",
            _ => "Session is active.",
        };
    }

    private void RefreshInteractiveStyles()
    {
        var palette = CurrentPalette;

        StyleButton(_controlViewButton, palette, "nav", !_isSettingsViewActive);
        StyleButton(_settingsViewButton, palette, "nav", _isSettingsViewActive);
        StyleButton(_toggleSidebarButton, palette, "toolbar");
        StyleButton(_showSidebarButton, palette, "toolbar");
        StyleButton(_toggleChatButton, palette, "toolbar", _isChatVisible);
        StyleButton(_toggleFullScreenButton, palette, "toolbar", _isFullScreen);
        StyleButton(_closeChatDrawerButton, palette, "toolbar");
        StyleButton(_sendChatButton, palette, "toolbar");
        StyleButton(_sendFileButton, palette, "action");
        StyleButton(_registerButton, palette, "action");
        StyleButton(_loginButton, palette, "action");
        StyleButton(_createRoomButton, palette, "action");
        StyleButton(_joinRoomButton, palette, "action");
        StyleButton(_disconnectButton, palette, "action");
        StyleToggle(_darkThemeCheckBox, palette);
    }
}
