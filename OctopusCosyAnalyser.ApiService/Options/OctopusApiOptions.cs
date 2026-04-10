namespace OctopusCosyAnalyser.ApiService.Options;

public sealed class OctopusApiOptions
{
    public const string SectionName = "OctopusApi";

    public string BackendApiUrl { get; set; } = "https://api.backend.octopus.energy/v1/graphql/";
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(90);
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(180);
}
