using client.Models;
using client.Networking;

namespace client.UI;

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

    private async Task<ServerSelection> ResolveServerSelectionAsync(string? roomId, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();

        var selection = await _loadBalancerApiClient.SelectServerAsync(
            ResolveLoadBalancerUrl(),
            _accessToken!,
            roomId,
            allowPrivateTcpEndpoint: CurrentMode == AppMode.Lan,
            cancellationToken);

        return selection with
        {
            TcpEndpoint = ResolveMainTcpEndpointForCurrentMode(selection.TcpEndpoint),
        };
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

    private string ResolveMainTcpEndpointForCurrentMode(string selectedEndpoint)
    {
        if (CurrentMode != AppMode.Lan)
        {
            return selectedEndpoint;
        }

        var separatorIndex = selectedEndpoint.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == selectedEndpoint.Length - 1)
        {
            throw new InvalidOperationException($"Load balancer returned an invalid TCP endpoint: {selectedEndpoint}");
        }

        var selectedPort = selectedEndpoint[(separatorIndex + 1)..].Trim();
        var lanHost = _lanServerIpTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(lanHost))
        {
            throw new InvalidOperationException("Enter the server IP address in the Network field.");
        }

        return $"{lanHost}:{selectedPort}";
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
