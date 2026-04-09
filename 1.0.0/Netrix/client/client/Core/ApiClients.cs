using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace client.Core;

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
                return detailElement.GetString() ?? $"Request failed with status {(int)response.StatusCode}.";
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

    public async Task<string> SelectServerAsync(string baseUrl, string bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/select-server");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? "Load balancer request failed." : detail);
        }

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Load balancer returned an empty response.");
        return payload.RootElement.GetProperty("ws_url").GetString()
            ?? throw new InvalidOperationException("Load balancer did not return a ws_url.");
    }
}

