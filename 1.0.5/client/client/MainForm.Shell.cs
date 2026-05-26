using System.Drawing;

namespace client;

partial class MainForm
{
    private void ShowControlView()
    {
        UpdateShellStateUi();
    }

    private void ShowSettingsView()
    {
        ShowControlView();
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
        _controlViewPanel.Visible = true;
        _settingsViewPanel.Visible = false;
        _chatDrawerPanel.Visible = _isChatVisible;
        _chatSplitter.Visible = _isChatVisible;

        _toggleSidebarButton.Text = "<";
        _showSidebarButton.Text = ">";
        _toggleChatButton.Text = _isChatVisible ? "Hide Chat" : "Chat";
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
        SetButtonInteractive(_toggleChatButton, true);
        SetButtonInteractive(_closeChatDrawerButton, true);
        SetButtonInteractive(_sendChatButton, hasSession);
        SetTextInputInteractive(_chatInputTextBox, hasSession);
        SetButtonInteractive(_disconnectButton, hasSession && !_isSessionActionInProgress);
        SetButtonInteractive(_sendFileButton, hasSession);
        SetButtonInteractive(_createRoomButton, IsAuthenticated && !hasSession && !_isSessionActionInProgress);
        SetButtonInteractive(_joinRoomButton, IsAuthenticated && !hasSession && !_isSessionActionInProgress);
        SetButtonInteractive(_logoutButton, IsAuthenticated && !_isSessionActionInProgress);
        _transferTitleLabel.Text = _transferListBox.Items.Count == 0
            ? "Transfer activity"
            : $"Transfer activity ({_transferListBox.Items.Count})";
        _authStatusLabel.Text = IsAuthenticated
            ? $"Signed in as {_authenticatedUsername}"
            : "Sign in required";
        RefreshInteractiveStyles();

        if (!hasSession)
        {
            _toolbarSessionLabel.Text = $"Mode: {modeText}";
            _roomStatusLabel.Text = "No active room";
            _remoteHintLabel.Text = "Create or join a room to start remote viewing and control.";
            return;
        }

        if (session is null)
        {
            return;
        }

        var roleText = FormatRole(session.Role);
        _toolbarSessionLabel.Text = $"Room {session.RoomId} | {roleText} | {modeText}";
        _roomStatusLabel.Text = $"Room {session.RoomId} | Role {roleText}";
        _remoteHintLabel.Text = session.Role switch
        {
            Core.ParticipantRole.Host => "Hosting this room. Remote input is only accepted from connected controllers.",
            Core.ParticipantRole.Controller when !session.CanSendControl => "Waiting for host approval. You can watch the stream, but remote input is blocked.",
            Core.ParticipantRole.Controller when _remoteInputActive => "Remote input is active. Use Escape to leave full screen.",
            Core.ParticipantRole.Controller => "Click inside the remote screen before sending mouse or keyboard input.",
            Core.ParticipantRole.Viewer => "Viewer mode is read-only. Rejoin as Controller to take control.",
            _ => "Session is active.",
        };
    }

    private void RefreshInteractiveStyles()
    {
        var palette = CurrentPalette;

        StyleButton(_showLoginPageButton, palette, "nav", _isLoginPageActive);
        StyleButton(_showRegisterPageButton, palette, "nav", !_isLoginPageActive);
        StyleButton(_controlViewButton, palette, "nav", true);
        StyleButton(_toggleSidebarButton, palette, "toolbar");
        StyleButton(_showSidebarButton, palette, "toolbar");
        StyleButton(_toggleChatButton, palette, "toolbar", _isChatVisible);
        StyleButton(_toggleFullScreenButton, palette, "toolbar", _isFullScreen);
        StyleButton(_closeChatDrawerButton, palette, "toolbar");
        StyleButton(_sendChatButton, palette, "toolbar");
        StyleButton(_sendFileButton, palette, "action");
        StyleButton(_logoutButton, palette, "action");
        StyleButton(_roomPasswordToggleButton, palette, "toolbar");
        StyleButton(_loginPasswordToggleButton, palette, "toolbar");
        StyleButton(_registerPasswordToggleButton, palette, "toolbar");
        StyleButton(_registerConfirmPasswordToggleButton, palette, "toolbar");
        StyleButton(_registerButton, palette, "action");
        StyleButton(_loginButton, palette, "action");
        StyleButton(_createRoomButton, palette, "action");
        StyleButton(_joinRoomButton, palette, "action");
        StyleButton(_disconnectButton, palette, "action");
        StyleToggle(_darkThemeCheckBox, palette);
    }
}
