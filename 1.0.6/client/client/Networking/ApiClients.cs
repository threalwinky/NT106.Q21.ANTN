using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;

using client.Models;

namespace client.Networking;

internal sealed class AuthApiClient
{
    private readonly HttpClient _httpClient = new();

    public Task<AuthTokenResponse> RegisterAsync(string baseUrl, string username, string password, CancellationToken cancellationToken)
    {
        return SendAuthRequestAsync($"{baseUrl.TrimEnd('/')}/register", username, password, cancellationToken);
    }

    public Task<AuthTokenResponse> LoginAsync(string baseUrl, string username, string password, CancellationToken cancellationToken)
    {
        return SendAuthRequestAsync($"{baseUrl.TrimEnd('/')}/login", username, password, cancellationToken);
    }

    public async Task LogoutAsync(string baseUrl, string bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
    }

    private async Task<AuthTokenResponse> SendAuthRequestAsync(string url, string username, string password, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            url,
            new
            {
                username,
                password,
            },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Authentication response is empty.");

        return new AuthTokenResponse(
            AccessToken: payload.RootElement.GetProperty("access_token").GetString() ?? string.Empty,
            Username: payload.RootElement.GetProperty("username").GetString() ?? username,
            ExpiresInMinutes: payload.RootElement.GetProperty("expires_in_minutes").GetInt32());
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await ReadDetailAsync(response, cancellationToken);
        throw new InvalidOperationException(detail);
    }

    private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
            if (payload is not null && payload.RootElement.TryGetProperty("detail", out var detailElement))
            {
                return detailElement.ValueKind switch
                {
                    JsonValueKind.String => detailElement.GetString() ?? $"Request failed with status {(int)response.StatusCode}.",
                    JsonValueKind.Array => string.Join(
                        Environment.NewLine,
                        detailElement.EnumerateArray()
                            .Select(item =>
                            {
                                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("msg", out var msgElement))
                                {
                                    return msgElement.GetString();
                                }

                                return item.ToString();
                            })
                            .Where(message => !string.IsNullOrWhiteSpace(message))),
                    _ => detailElement.ToString(),
                };
            }
        }
        catch
        {
        }

        return $"Request failed with status {(int)response.StatusCode}.";
    }
}

internal sealed class LoadBalancerApiClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<ServerSelection> SelectServerAsync(
        string baseUrl,
        string bearerToken,
        string? roomId,
        bool allowPrivateTcpEndpoint,
        CancellationToken cancellationToken)
    {
        var requestUrl = $"{baseUrl.TrimEnd('/')}/select-server";
        if (!string.IsNullOrWhiteSpace(roomId))
        {
            requestUrl = $"{requestUrl}?room_id={Uri.EscapeDataString(roomId)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? "Load balancer request failed." : detail);
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Load balancer returned an empty response.");
        var tcpEndpoint = payload.RootElement.TryGetProperty("tcp_endpoint", out var tcpElement)
            ? tcpElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(tcpEndpoint))
        {
            throw new InvalidOperationException("Load balancer did not return a tcp_endpoint.");
        }

        ValidateTcpEndpoint(tcpEndpoint, allowPrivateTcpEndpoint);
        return new ServerSelection(tcpEndpoint);
    }

    private static void ValidateTcpEndpoint(string endpoint, bool allowPrivateEndpoint)
    {
        var separatorIndex = endpoint.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == endpoint.Length - 1)
        {
            throw new InvalidOperationException($"Load balancer returned an invalid tcp_endpoint: {endpoint}");
        }

        var host = endpoint[..separatorIndex].Trim();
        if (!int.TryParse(endpoint[(separatorIndex + 1)..], out var port) || port <= 0 || port > 65535)
        {
            throw new InvalidOperationException($"Load balancer returned an invalid TCP port: {endpoint}");
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            if (!allowPrivateEndpoint)
            {
                throw new InvalidOperationException("Load balancer returned localhost. Configure the main server with a public TCP endpoint.");
            }

            return;
        }

        if (!IPAddress.TryParse(host, out var address))
        {
            return;
        }

        if (!allowPrivateEndpoint && (IPAddress.IsLoopback(address) || IsPrivateAddress(address)))
        {
            throw new InvalidOperationException(
                $"Load balancer returned a private TCP endpoint ({endpoint}). Configure NETRIX_PUBLIC_TCP_ENDPOINT on the main server.");
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254);
    }
}
