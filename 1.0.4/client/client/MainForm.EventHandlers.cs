using client.Core;

namespace client;

partial class MainForm
{
    private void WireEvents()
    {
        _showLoginPageButton.Click += (_, _) => ShowLoginPage();
        _showRegisterPageButton.Click += (_, _) => ShowRegisterPage();
        _controlViewButton.Click += (_, _) => ShowControlView();
        _toggleSidebarButton.Click += (_, _) => ToggleSidebar();
        _showSidebarButton.Click += (_, _) => ToggleSidebar(true);
        _toggleChatButton.Click += (_, _) => ToggleChatDrawer();
        _toggleFullScreenButton.Click += (_, _) => ToggleFullScreen();
        _closeChatDrawerButton.Click += (_, _) => ToggleChatDrawer(false);
        _logoutButton.Click += async (_, _) =>
        {
            if (!IsButtonInteractive(_logoutButton))
            {
                return;
            }

            await LogoutAsync();
        };
        _darkThemeCheckBox.CheckedChanged += (_, _) => ApplyTheme();

        _modeComboBox.SelectedIndexChanged += (_, _) => UpdateModeUi();
        _registerButton.Click += async (_, _) => await RegisterAsync();
        _loginButton.Click += async (_, _) => await LoginAsync();
        _createRoomButton.Click += async (_, _) =>
        {
            if (!IsButtonInteractive(_createRoomButton))
            {
                return;
            }

            await CreateRoomAsync();
        };
        _joinRoomButton.Click += async (_, _) =>
        {
            if (!IsButtonInteractive(_joinRoomButton))
            {
                return;
            }

            await JoinRoomAsync();
        };
        _disconnectButton.Click += async (_, _) =>
        {
            if (!IsButtonInteractive(_disconnectButton))
            {
                return;
            }

            await DisconnectAsync("Disconnected by user.");
        };
        _sendFileButton.Click += async (_, _) =>
        {
            if (!IsButtonInteractive(_sendFileButton))
            {
                return;
            }

            await SendFileAsync();
        };
        _sendChatButton.Click += async (_, _) =>
        {
            if (!IsButtonInteractive(_sendChatButton))
            {
                return;
            }

            await SendChatAsync();
        };
        _chatInputTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && IsTextInputInteractive(_chatInputTextBox))
            {
                e.SuppressKeyPress = true;
                await SendChatAsync();
            }
        };
        _passwordTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await LoginAsync();
            }
        };
        _registerConfirmPasswordTextBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await RegisterAsync();
            }
        };

        _remoteScreenBox.MouseClick += (_, _) =>
        {
            _remoteInputActive = _netrixClient.CurrentSession?.Role == ParticipantRole.Controller
                && _netrixClient.CurrentSession?.CanSendControl == true;
            _remoteScreenBox.Focus();
            RefreshSessionChrome();
        };
        _remoteScreenBox.Leave += async (_, _) => await ReleaseRemoteKeyboardAsync();
        _remoteScreenBox.MouseMove += async (_, e) => await SendMouseMoveAsync(e);
        _remoteScreenBox.MouseDown += async (_, e) => await SendMouseButtonAsync("mouse_down", e);
        _remoteScreenBox.MouseUp += async (_, e) => await SendMouseButtonAsync("mouse_up", e);
        _remoteScreenBox.MouseWheel += async (_, e) => await SendMouseWheelAsync(e);

        KeyDown += async (_, e) => await SendKeyAsync("key_down", e);
        KeyUp += async (_, e) => await SendKeyAsync("key_up", e);
        Deactivate += async (_, _) =>
        {
            await ReleaseRemoteKeyboardAsync();
            _remoteInputActive = false;
            RefreshSessionChrome();
        };

        _netrixClient.StatusChanged += _ => { };
        _netrixClient.ErrorReceived += message => OnUiThread(() =>
        {
            if (!FailPendingRoomRequest(message))
            {
                ShowErrorDialog(message);
            }
        });
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
        _netrixClient.ControlRequestReceived += request => OnUiThread(async () =>
        {
            if (_netrixClient.CurrentSession?.Role != ParticipantRole.Host)
            {
                return;
            }

            var result = MessageBox.Show(
                this,
                $"{request.DisplayName} wants controller access for room {request.RoomId}. Allow remote input?",
                "Controller Request",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            await RespondToControlRequestAsync(request, result == DialogResult.Yes);
        });
        _netrixClient.ControllerPermissionChanged += info => OnUiThread(() =>
        {
            _remoteInputActive = _netrixClient.CurrentSession?.Role == ParticipantRole.Controller
                && _netrixClient.CurrentSession?.CanSendControl == true;
            RefreshSessionChrome();
        });
        _netrixClient.RoomClosed += detail => OnUiThread(async () =>
        {
            var handledPendingRequest = FailPendingRoomRequest(detail);
            if (!handledPendingRequest)
            {
                ShowErrorDialog(detail, "Session Closed");
            }

            await DisconnectAsync(detail);
        });
    }
}
