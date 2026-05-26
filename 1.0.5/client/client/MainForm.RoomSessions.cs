using client.Core;

namespace client;

partial class MainForm
{
    private async Task CreateRoomAsync()
    {
        await _sessionActionLock.WaitAsync();
        try
        {
            SetSessionActionBusy(true, "Creating room...");
            EnsureAuthenticated();
            ValidateRoomPassword();

            using var cts = CreateShortTimeout();
            var roomReadyTask = BeginPendingRoomRequest("Create room", cts.Token);
            var serverUrl = await ResolveServerUrlAsync(null, cts.Token);
            await EnsureConnectedAsync(serverUrl, cts.Token);
            await _netrixClient.CreateRoomAsync(
                displayName: ResolveDisplayName(),
                roomPassword: _roomPasswordTextBox.Text,
                mode: CurrentMode,
                token: _accessToken,
                cancellationToken: cts.Token);
            await roomReadyTask;
        }
        catch (Exception ex)
        {
            FailPendingRoomRequest(ex.Message);
            ShowErrorDialog(ex.Message);
        }
        finally
        {
            SetSessionActionBusy(false);
            _sessionActionLock.Release();
        }
    }

    private async Task JoinRoomAsync()
    {
        await _sessionActionLock.WaitAsync();
        try
        {
            SetSessionActionBusy(true, "Joining room...");
            EnsureAuthenticated();
            if (string.IsNullOrWhiteSpace(_roomIdTextBox.Text))
            {
                throw new InvalidOperationException("Room ID is required.");
            }

            ValidateRoomPassword();

            using var cts = CreateShortTimeout();
            var roomReadyTask = BeginPendingRoomRequest("Join room", cts.Token);
            var roomId = _roomIdTextBox.Text.Trim().ToUpperInvariant();
            _roomIdTextBox.Text = roomId;
            PrepareRoomSecurityForJoin(roomId, _roomPasswordTextBox.Text);
            var serverUrl = await ResolveServerUrlAsync(roomId, cts.Token);
            await EnsureConnectedAsync(serverUrl, cts.Token);
            await _netrixClient.JoinRoomAsync(
                roomId: roomId,
                displayName: ResolveDisplayName(),
                roomPassword: _roomPasswordTextBox.Text,
                role: SelectedJoinRole,
                mode: CurrentMode,
                token: _accessToken,
                cancellationToken: cts.Token);
            await roomReadyTask;
        }
        catch (Exception ex)
        {
            if (_netrixClient.CurrentSession is null)
            {
                _netrixClient.ClearRoomSecurity();
            }

            FailPendingRoomRequest(ex.Message);
            ShowErrorDialog(ex.Message);
        }
        finally
        {
            SetSessionActionBusy(false);
            _sessionActionLock.Release();
        }
    }

    private async Task DisconnectAsync(string reason)
    {
        await _sessionActionLock.WaitAsync();
        try
        {
            SetSessionActionBusy(true);
            await ReleaseRemoteKeyboardAsync();
            FailPendingRoomRequest(reason);
            StopHostStreaming();
            StopPingLoop();
            _remoteInputActive = false;
            _selectedServerUrl = null;
            _isChatVisible = false;
            _roomStatusLabel.Text = "No active room";
            _roomIdTextBox.Clear();
            _participantsListBox.Items.Clear();
            _participantsTitleLabel.Text = "Connected peers";
            _chatListBox.Items.Clear();
            _transferListBox.Items.Clear();
            var oldImage = _remoteScreenBox.Image;
            _remoteScreenBox.Image = null;
            oldImage?.Dispose();
            ClearIncomingTransfers();
            await _netrixClient.DisconnectAsync();
            RefreshSessionChrome();
            UpdateShellStateUi();
            UpdateStatus(reason);
        }
        finally
        {
            SetSessionActionBusy(false);
            _sessionActionLock.Release();
        }
    }

    private void PrepareRoomSecurityForJoin(string roomId, string roomPassword)
    {
        if (!UseRoomEncryption || string.IsNullOrWhiteSpace(roomPassword))
        {
            _netrixClient.ClearRoomSecurity();
            return;
        }

        _netrixClient.ConfigureRoomSecurity(roomId, roomPassword, enabled: true);
    }

    private Task<RoomSessionInfo> BeginPendingRoomRequest(string actionName, CancellationToken cancellationToken)
    {
        var request = new TaskCompletionSource<RoomSessionInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRoomRequest = request;
        _pendingRoomRequestName = actionName;

        cancellationToken.Register(() =>
        {
            if (!ReferenceEquals(_pendingRoomRequest, request))
            {
                return;
            }

            _pendingRoomRequest = null;
            _pendingRoomRequestName = null;
            request.TrySetException(new TimeoutException($"{actionName} timed out. Check the server connection, room ID, and room password, then try again."));
        });

        return request.Task;
    }

    private void CompletePendingRoomRequest(RoomSessionInfo session)
    {
        if (_pendingRoomRequest is null)
        {
            return;
        }

        var request = _pendingRoomRequest;
        _pendingRoomRequest = null;
        _pendingRoomRequestName = null;
        request.TrySetResult(session);
    }

    private bool FailPendingRoomRequest(string detail)
    {
        if (_pendingRoomRequest is null)
        {
            return false;
        }

        var request = _pendingRoomRequest;
        var actionName = _pendingRoomRequestName ?? "Room request";
        _pendingRoomRequest = null;
        _pendingRoomRequestName = null;
        request.TrySetException(new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"{actionName} failed." : detail));
        return true;
    }
}
