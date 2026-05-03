namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Summary of upstream API connection health — surfaced in the UI as a banner so the user
/// can immediately see if Octopus Energy credentials are missing/invalid or AI features
/// are unavailable due to a missing Anthropic API key.
/// </summary>
public sealed class ApiStatusDto
{
    /// <summary>True when at least one OctopusAccountSettings row exists.</summary>
    public bool HasSettings { get; set; }

    /// <summary>The first account number found (single-user app), null when none configured.</summary>
    public string? AccountNumber { get; set; }

    /// <summary>"apikey" or "password" — what the saved settings declare.</summary>
    public string? AuthMode { get; set; }

    /// <summary>True when the saved settings contain the credentials required for the chosen auth mode.</summary>
    public bool OctopusCredentialsConfigured { get; set; }

    /// <summary>True when the most recent token acquisition against the Octopus auth endpoint succeeded.</summary>
    public bool OctopusAuthOk { get; set; }

    /// <summary>Human-readable error message when OctopusAuthOk is false.</summary>
    public string? OctopusAuthError { get; set; }

    /// <summary>True when an Anthropic API key is available either per-account (DB) or globally (config/env).</summary>
    public bool AnthropicConfigured { get; set; }

    /// <summary>Where the Anthropic key was sourced from: "account", "config", or null when not configured.</summary>
    public string? AnthropicKeySource { get; set; }

    /// <summary>True when at least one heat pump device has been discovered and registered.</summary>
    public bool HasDevice { get; set; }

    /// <summary>UTC timestamp of when this status snapshot was generated.</summary>
    public DateTime CheckedAt { get; set; }
}
