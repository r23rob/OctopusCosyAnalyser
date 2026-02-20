using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace OctopusCosyAnalyser.ApiService.Services;

public class TadoClient
{
    private sealed record TokenCacheEntry(string AccessToken, DateTime ExpiresAt);

    private static readonly ConcurrentDictionary<string, TokenCacheEntry> TokenCache = new();

    private const string TokenEndpoint = "https://login.tado.com/oauth2/token";
    private const string ApiBaseUrl = "https://my.tado.com/api/v2";

    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public TadoClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _clientId = configuration["Tado:ClientId"] ?? "tado-web-app";
        _clientSecret = configuration["Tado:ClientSecret"]
            ?? throw new InvalidOperationException("Tado:ClientSecret is not configured.");
    }

    // ── Authentication ───────────────────────────────────────────────

    private async Task<string> GetAccessTokenAsync(string username, string password)
    {
        var cacheKey = $"{username}";
        if (TokenCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            return cached.AccessToken;

        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "home.user"
        });

        var response = await _httpClient.PostAsync(TokenEndpoint, formContent);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(result);
        var token = json.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Tado access token was empty.");

        var expiresIn = json.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 600;
        var entry = new TokenCacheEntry(token, DateTime.UtcNow.AddSeconds(expiresIn - 30));
        TokenCache[cacheKey] = entry;

        return token;
    }

    // ── Homes ────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the list of homes associated with the Tado account.
    /// </summary>
    public async Task<JsonDocument> GetMeAsync(string username, string password)
    {
        return await ExecuteGetAsync(username, password, $"{ApiBaseUrl}/me");
    }

    // ── Zones ────────────────────────────────────────────────────────

    /// <summary>
    /// Gets all zones for a home.
    /// </summary>
    public async Task<JsonDocument> GetZonesAsync(string username, string password, long homeId)
    {
        return await ExecuteGetAsync(username, password, $"{ApiBaseUrl}/homes/{homeId}/zones");
    }

    /// <summary>
    /// Gets the current state (temperature, humidity, setpoint, heating) for a zone.
    /// </summary>
    public async Task<JsonDocument> GetZoneStateAsync(string username, string password, long homeId, int zoneId)
    {
        return await ExecuteGetAsync(username, password, $"{ApiBaseUrl}/homes/{homeId}/zones/{zoneId}/state");
    }

    // ── Transport ────────────────────────────────────────────────────

    private async Task<JsonDocument> ExecuteGetAsync(string username, string password, string url)
    {
        var token = await GetAccessTokenAsync(username, password);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Tado API returned {(int)response.StatusCode}: {result}");

        return JsonDocument.Parse(result);
    }
}
