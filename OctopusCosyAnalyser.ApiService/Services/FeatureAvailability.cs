using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

/// <summary>
/// Indicates which features are available based on infrastructure configuration.
/// When no database connection string is provided, the API runs in "lite mode" —
/// live data from the Octopus API works, but history, snapshots, and settings require
/// the full deployment with PostgreSQL.
/// </summary>
public sealed class FeatureAvailability
{
    public bool DatabaseAvailable { get; init; }

    /// <summary>Environment variable fallback: OCTOPUS_ACCOUNT_NUMBER.</summary>
    public string? FallbackAccountNumber { get; init; }

    /// <summary>Environment variable fallback: OCTOPUS_API_KEY.</summary>
    public string? FallbackApiKey { get; init; }

    /// <summary>Environment variable fallback: OCTOPUS_EUID.</summary>
    public string? FallbackEuid { get; init; }

    public bool HasFallbackCredentials =>
        !string.IsNullOrWhiteSpace(FallbackAccountNumber)
        && !string.IsNullOrWhiteSpace(FallbackApiKey);

    public bool History => DatabaseAvailable;
    public bool Efficiency => DatabaseAvailable;
    public bool Settings => DatabaseAvailable;
    public bool LiveData => true;

    /// <summary>
    /// Creates a synthetic <see cref="OctopusAccountSettings"/> from environment variable
    /// fallback credentials, for use in lite mode when no database is available.
    /// </summary>
    public OctopusAccountSettings CreateFallbackSettings() => new()
    {
        AccountNumber = FallbackAccountNumber ?? string.Empty,
        ApiKey = FallbackApiKey ?? string.Empty,
        AuthMode = "apikey"
    };
}
