using client.Core;

namespace client;

partial class Form1
{
    private void WireEvents()
    {
        _controlViewButton.Click += (_, _) => ShowControlView();
        _settingsViewButton.Click += (_, _) => ShowSettingsView();
        _toggleSidebarButton.Click += (_, _) => ToggleSidebar();
        _showSidebarButton.Click += (_, _) => ToggleSidebar(true);
        _toggleChatButton.Click += (_, _) => ToggleChatDrawer();
        _toggleFullScreenButton.Click += (_, _) => ToggleFullScreen();
        _closeChatDrawerButton.Click += (_, _) => ToggleChatDrawer(false);
        _darkThemeCheckBox.CheckedChanged += (_, _) => ApplyTheme();

        _modeComboBox.SelectedIndexChanged += (_, _) => UpdateModeUi();
        _registerButton.Click += async (_, _) => await RegisterAsync();
        _loginButton.Click += async (_, _) => await LoginAsync();
        _createRoomButton.Click += async (_, _) => await CreateRoomAsync();
        _joinRoomButton.Click += async (_, _) => await JoinRoomAsync();
        _disconnectButton.Click += async (_, _) => await DisconnectAsync("Disconnected by user.");
        _sendFileButton.Click += async (_, _) => await SendFileAsync();
        _sendChatButton.Click += async (_, _) => await SendChatAsync();
        _chatInputTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SendChatAsync();
            }
        };

        _remoteScreenBox.MouseClick += (_, _) =>
        {
            _remoteInputActive = _netrixClient.CurrentSession?.Role == ParticipantRole.Controller;
            _remoteScreenBox.Focus();
            RefreshSessionChrome();
        };
        _remoteScreenBox.MouseMove += async (_, e) => await SendMouseMoveAsync(e);
        _remoteScreenBox.MouseDown += async (_, e) => await SendMouseButtonAsync("mouse_down", e);
        _remoteScreenBox.MouseUp += async (_, e) => await SendMouseButtonAsync("mouse_up", e);
        _remoteScreenBox.MouseWheel += async (_, e) => await SendMouseWheelAsync(e);

        KeyDown += async (_, e) => await SendKeyAsync("key_down", e);
        KeyUp += async (_, e) => await SendKeyAsync("key_up", e);
        Deactivate += (_, _) =>
        {
            _remoteInputActive = false;
            RefreshSessionChrome();
        };

        _netrixClient.StatusChanged += message => OnUiThread(() => UpdateStatus(message));
        _netrixClient.ErrorReceived += message => OnUiThread(() => UpdateStatus($"Error: {message}"));
        _netrixClient.RoomReady += session => OnUiThread(() => HandleRoomReady(session));
        _netrixClient.ParticipantsUpdated += participants => OnUiThread(() => RenderParticipants(participants));
        _netrixClient.FrameReceived += frame => OnUiThread(() => RenderFrame(frame));
        _netrixClient.ChatReceived += message => OnUiThread(() =>
        {
            _chatListBox.Items.Add($"{message.Sender} ({message.Role}): {message.Text}");
            _chatListBox.TopIndex = _chatListBox.Items.Count - 1;
            RefreshSessionChrome();
        });
        _netrixClient.FileOfferReceived += offer => HandleIncomingFileOffer(offer);
        _netrixClient.FileChunkReceived += chunk => HandleIncomingFileChunk(chunk);
        _netrixClient.FileTransferCompleted += complete => HandleIncomingFileComplete(complete);
        _netrixClient.InputReceived += command => Task.Run(() => RemoteInputExecutor.Apply(command));
        _netrixClient.RoomClosed += detail => OnUiThread(async () =>
        {
            UpdateStatus(detail);
            await DisconnectAsync(detail);
        });
    }
}
