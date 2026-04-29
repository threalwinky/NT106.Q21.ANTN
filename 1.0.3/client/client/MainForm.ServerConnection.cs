using client.Core;

namespace client;

partial class MainForm
{
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
            ShowErrorDialog(ex.Message);
        }
    }

    private async Task<string> ResolveServerUrlAsync(string? roomId, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        if (CurrentMode == AppMode.Lan)
        {
            return ResolveLanMainServerUrl();
        }

        return await _loadBalancerApiClient.SelectServerAsync(NetrixEndpoints.LoadBalancer, _accessToken!, roomId, cancellationToken);
    }

    private string ResolveLanMainServerUrl()
    {
        var serverUrl = _lanMainServerTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new InvalidOperationException("Enter the LAN main-server WebSocket URL, for example ws://192.168.131.1/ws.");
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("LAN main-server URL is not a valid absolute WebSocket URL.");
        }

        if (uri.Scheme is not ("ws" or "wss"))
        {
            throw new InvalidOperationException("LAN main-server URL must start with ws:// or wss://.");
        }

        return uri.ToString();
    }

    private async Task EnsureConnectedAsync(string serverUrl, CancellationToken cancellationToken)
    {
        if (_netrixClient.HasHealthyConnection && string.Equals(_selectedServerUrl, serverUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        UpdateStatus($"Connecting to {serverUrl}...");
        await _netrixClient.ConnectAsync(serverUrl, cancellationToken);
        _selectedServerUrl = serverUrl;
        StartPingLoop();
    }

    private async Task RespondToControlRequestAsync(ControlRequestInfo request, bool approved)
    {
        try
        {
            using var cts = CreateShortTimeout();
            await _netrixClient.SendControlDecisionAsync(request.TargetClientId, approved, cts.Token);
        }
        catch (Exception ex)
        {
            ShowErrorDialog(ex.Message);
        }
    }
}
