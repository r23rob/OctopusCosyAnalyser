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
/// For API key mode, returns the key directly.
/// For password mode, obtains a JWT via the obtainKrakenToken mutation and caches it for 55 minutes.
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
        if (settings.AuthMode == "apikey")
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new ArgumentException("API key is required for API key authentication.");
            return settings.ApiKey;
        }

        if (string.IsNullOrWhiteSpace(settings.Email))
            throw new ArgumentException("Email is required for password authentication.");
        if (string.IsNullOrWhiteSpace(settings.OctopusPassword))
            throw new ArgumentException("Password is required for password authentication.");

        var cacheKey = $"{settings.Email}:{settings.OctopusPassword}";

        // Fast path: return cached token without acquiring lock
        if (TokenCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            return cached.Token;

        await TokenSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (TokenCache.TryGetValue(cacheKey, out cached) && DateTime.UtcNow < cached.ExpiresAt)
                return cached.Token;

            var query = """
            mutation KrakenTokenAuthentication($email: String!, $password: String!) {
              obtainKrakenToken(input: { email: $email, password: $password }) {
                token
              }
            }
            """;

            var variables = new { email = settings.Email, password = settings.OctopusPassword };
            var payload = JsonSerializer.Serialize(new { query, variables });

            using var client = _httpClientFactory.CreateClient();
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.octopus.energy/v1/graphql/", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(result);
            var token = json.RootElement
                .GetProperty("data")
                .GetProperty("obtainKrakenToken")
                .GetProperty("token")
                .GetString()!;

            TokenCache[cacheKey] = new TokenCacheEntry(token, DateTime.UtcNow.AddMinutes(55));
            _logger.LogDebug("Obtained and cached Octopus auth token for {Email}", settings.Email);

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
