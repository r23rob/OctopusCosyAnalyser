using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

/// <summary>
/// Indicates which features are available. With DynamoDB, all features are always
/// available (no "lite mode"). Fallback credentials are kept for the /summary
/// endpoint which can work without stored settings.
/// </summary>
public sealed class FeatureAvailability
{
    /// <summary>Always true — DynamoDB is always available.</summary>
    public bool DatabaseAvailable { get; init; } = true;

    /// <summary>Environment variable fallback: OCTOPUS_ACCOUNT_NUMBER.</summary>
    public string? FallbackAccountNumber { get; init; }

    /// <summary>Environment variable fallback: OCTOPUS_API_KEY.</summary>
    public string? FallbackApiKey { get; init; }

    /// <summary>Environment variable fallback: OCTOPUS_EUID.</summary>
    public string? FallbackEuid { get; init; }

    public bool HasFallbackCredentials =>
        !string.IsNullOrWhiteSpace(FallbackAccountNumber)
        && !string.IsNullOrWhiteSpace(FallbackApiKey);

    public bool History => true;
    public bool Efficiency => true;
    public bool Settings => true;
    public bool LiveData => true;

    /// <summary>
    /// Creates a synthetic <see cref="OctopusAccountSettings"/> from environment variable
    /// fallback credentials, for use when no stored settings exist.
    /// </summary>
    public OctopusAccountSettings CreateFallbackSettings() => new()
    {
        AccountNumber = FallbackAccountNumber ?? string.Empty,
        ApiKey = FallbackApiKey ?? string.Empty,
        AuthMode = "apikey"
    };
}
