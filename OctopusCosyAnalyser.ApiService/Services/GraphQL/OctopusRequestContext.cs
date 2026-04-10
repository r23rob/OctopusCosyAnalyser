using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services.GraphQL;

/// <summary>
/// Ambient context for the current Octopus account settings.
/// Set this before making ZeroQL queries so the auth handler knows which credentials to use.
/// Uses AsyncLocal to safely flow across async continuations without leaking between requests.
/// </summary>
public static class OctopusRequestContext
{
    private static readonly AsyncLocal<OctopusAccountSettings?> _current = new();

    public static OctopusAccountSettings? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
