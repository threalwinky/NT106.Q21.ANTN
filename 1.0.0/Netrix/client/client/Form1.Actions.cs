using client.Core;

namespace client;

partial class Form1
{
    private async Task RegisterAsync()
    {
        try
        {
            using var cts = CreateShortTimeout();
            var response = await _authApiClient.RegisterAsync(_authUrlTextBox.Text.Trim(), _usernameTextBox.Text.Trim(), _passwordTextBox.Text, cts.Token);
            _accessToken = response.AccessToken;
            _authStatusLabel.Text = $"Registered as {response.Username}";
            UpdateStatus("Internet authentication token issued.");
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
        }
    }

    private async Task LoginAsync()
    {
        try
        {
            using var cts = CreateShortTimeout();
            var response = await _authApiClient.LoginAsync(_authUrlTextBox.Text.Trim(), _usernameTextBox.Text.Trim(), _passwordTextBox.Text, cts.Token);
            _accessToken = response.AccessToken;
            _authStatusLabel.Text = $"Logged in as {response.Username}";
            UpdateStatus("Login completed.");
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
        }
    }

    private async Task CreateRoomAsync()
    {
        try
        {
            ValidateRoomPassword();

            using var cts = CreateShortTimeout();
            var serverUrl = await ResolveServerUrlAsync(cts.Token);
            await EnsureConnectedAsync(serverUrl, cts.Token);
            await _netrixClient.CreateRoomAsync(
                displayName: ResolveDisplayName(),
                roomPassword: _roomPasswordTextBox.Text,
                mode: CurrentMode,
                token: _accessToken,
                cancellationToken: cts.Token);
            UpdateStatus("Create room request sent.");
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
        }
    }

    private async Task JoinRoomAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_roomIdTextBox.Text))
            {
                throw new InvalidOperationException("Room ID is required.");
            }

            ValidateRoomPassword();

            using var cts = CreateShortTimeout();
            var serverUrl = await ResolveServerUrlAsync(cts.Token);
            await EnsureConnectedAsync(serverUrl, cts.Token);
            await _netrixClient.JoinRoomAsync(
                roomId: _roomIdTextBox.Text.Trim(),
                displayName: ResolveDisplayName(),
                roomPassword: _roomPasswordTextBox.Text,
                role: SelectedJoinRole,
                mode: CurrentMode,
                token: _accessToken,
                cancellationToken: cts.Token);
            UpdateStatus("Join room request sent.");
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
        }
    }

    private async Task DisconnectAsync(string reason)
    {
        StopHostStreaming();
        StopPingLoop();
        _remoteInputActive = false;
        _selectedServerUrl = null;
        _roomStatusLabel.Text = "No active room";
        _roomIdTextBox.Clear();
        _participantsListBox.Items.Clear();
        _chatListBox.Items.Clear();
        var oldImage = _remoteScreenBox.Image;
        _remoteScreenBox.Image = null;
        oldImage?.Dispose();
        await _netrixClient.DisconnectAsync();
        UpdateStatus(reason);
    }

    private async Task SendChatAsync()
    {
        if (string.IsNullOrWhiteSpace(_chatInputTextBox.Text) || _netrixClient.CurrentSession is null)
        {
            return;
        }

        try
        {
            using var cts = CreateShortTimeout();
            var text = _chatInputTextBox.Text.Trim();
            await _netrixClient.SendChatAsync(text, cts.Token);
            _chatInputTextBox.Clear();
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message);
        }
    }

    private async Task<string> ResolveServerUrlAsync(CancellationToken cancellationToken)
    {
        if (CurrentMode == AppMode.Lan)
        {
            var lanUrl = _lanServerUrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(lanUrl))
            {
                throw new InvalidOperationException("LAN WebSocket URL is required.");
            }

            return lanUrl;
        }

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            throw new InvalidOperationException("Internet mode requires a valid JWT. Use Register or Login first.");
        }

        return await _loadBalancerApiClient.SelectServerAsync(_loadBalancerUrlTextBox.Text.Trim(), _accessToken, cancellationToken);
    }

    private async Task EnsureConnectedAsync(string serverUrl, CancellationToken cancellationToken)
    {
        if (_netrixClient.IsConnected && string.Equals(_selectedServerUrl, serverUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _selectedServerUrl = serverUrl;
        await _netrixClient.ConnectAsync(serverUrl, cancellationToken);
        StartPingLoop();
    }

    private void HandleRoomReady(RoomSessionInfo session)
    {
        _roomIdTextBox.Text = session.RoomId;
        _roomStatusLabel.Text = $"Room {session.RoomId} | Role {session.Role}";
        _chatListBox.Items.Clear();
        _remoteInputActive = session.Role == ParticipantRole.Controller;

        if (session.Role == ParticipantRole.Host)
        {
            StartHostStreaming();
            UpdateStatus("Hosting room. Share room ID and password with peers.");
        }
        else
        {
            StopHostStreaming();
            UpdateStatus("Joined room successfully.");
        }
    }

    private void RenderParticipants(IReadOnlyList<ParticipantInfo> participants)
    {
        _participantsListBox.Items.Clear();
        foreach (var participant in participants)
        {
            _participantsListBox.Items.Add($"{participant.DisplayName} [{participant.Role}]");
        }
    }

    private void RenderFrame(RemoteFrame frame)
    {
        using var stream = new MemoryStream(frame.JpegBytes);
        using var image = Image.FromStream(stream);
        var clonedImage = new Bitmap(image);

        var oldImage = _remoteScreenBox.Image;
        _remoteScreenBox.Image = clonedImage;
        oldImage?.Dispose();
    }
}

