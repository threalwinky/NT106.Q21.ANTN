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
        var ip = _lanServerIpTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            throw new InvalidOperationException("Enter the server IP address in the Network field on the login page.");
        }

        return $"ws://{ip}:8000/ws";
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
