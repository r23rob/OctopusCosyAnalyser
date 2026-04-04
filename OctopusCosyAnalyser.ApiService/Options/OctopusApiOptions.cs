namespace OctopusCosyAnalyser.ApiService.Options;

public sealed class OctopusApiOptions
{
    public const string SectionName = "OctopusApi";

    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(90);
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(180);
}
