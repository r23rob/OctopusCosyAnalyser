using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services.GraphQL;

public interface IOctopusTokenService
{
    Task<string> GetAuthTokenAsync(OctopusAccountSettings settings, CancellationToken cancellationToken = default);
    void EvictToken(OctopusAccountSettings settings);
}

/// <summary>
/// Manages Octopus Energy API authentication tokens.
/// Both API key mode and password mode obtain a JWT via the obtainKrakenToken mutation
/// and cache it for 55 minutes — the backend GraphQL endpoint requires a JWT, not the
/// raw API key.
/// </summary>
public class OctopusTokenService : IOctopusTokenService
{
    private sealed record TokenCacheEntry(string Token, DateTime ExpiresAt);

    private static readonly ConcurrentDictionary<string, TokenCacheEntry> TokenCache = new();
    private static readonly SemaphoreSlim TokenSemaphore = new(1, 1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OctopusTokenService> _logger;

    public OctopusTokenService(IHttpClientFactory httpClientFactory, ILogger<OctopusTokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAuthTokenAsync(OctopusAccountSettings settings, CancellationToken cancellationToken = default)
    {
        string cacheKey;
        string query;
        object variables;

        if (settings.AuthMode == "apikey")
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new ArgumentException("API key is required for API key authentication.");

            cacheKey = $"apikey:{settings.ApiKey}";
            query = """
            mutation KrakenTokenAuthentication($apiKey: String!) {
              obtainKrakenToken(input: { APIKey: $apiKey }) {
                token
              }
            }
            """;
            variables = new { apiKey = settings.ApiKey };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.Email))
                throw new ArgumentException("Email is required for password authentication.");
            if (string.IsNullOrWhiteSpace(settings.OctopusPassword))
                throw new ArgumentException("Password is required for password authentication.");

            cacheKey = $"{settings.Email}:{settings.OctopusPassword}";
            query = """
            mutation KrakenTokenAuthentication($email: String!, $password: String!) {
              obtainKrakenToken(input: { email: $email, password: $password }) {
                token
              }
            }
            """;
            variables = new { email = settings.Email, password = settings.OctopusPassword };
        }

        // Fast path: return cached token without acquiring lock
        if (TokenCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            return cached.Token;

        await TokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (TokenCache.TryGetValue(cacheKey, out cached) && DateTime.UtcNow < cached.ExpiresAt)
                return cached.Token;

            var payload = JsonSerializer.Serialize(new { query, variables });

            using var client = _httpClientFactory.CreateClient();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.octopus.energy/v1/graphql/", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(result);
            
            // Check for GraphQL errors before accessing data
            if (json.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
            {
                var errorMsg = errors[0].TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                _logger.LogError("Failed to obtain Kraken token: {Error}. Response: {Response}", errorMsg, result);
                throw new InvalidOperationException($"Failed to obtain Kraken token: {errorMsg}");
            }
            
            if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            {
                _logger.LogError("Failed to obtain Kraken token: no data in response. Response: {Response}", result);
                throw new InvalidOperationException($"Failed to obtain Kraken token: no data in response. Response: {result}");
            }
            
            var token = data
                .GetProperty("obtainKrakenToken")
                .GetProperty("token")
                .GetString()!;

            TokenCache[cacheKey] = new TokenCacheEntry(token, DateTime.UtcNow.AddMinutes(55));
            _logger.LogDebug("Obtained and cached Octopus auth token using {AuthMode} mode", settings.AuthMode);

            return token;
        }
        finally
        {
            TokenSemaphore.Release();
        }
    }

    public void EvictToken(OctopusAccountSettings settings)
    {
        var cacheKey = settings.AuthMode == "apikey"
            ? $"apikey:{settings.ApiKey}"
            : $"{settings.Email}:{settings.OctopusPassword}";
        TokenCache.TryRemove(cacheKey, out _);
    }
}
