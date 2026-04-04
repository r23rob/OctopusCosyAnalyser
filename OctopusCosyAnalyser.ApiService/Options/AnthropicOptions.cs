namespace OctopusCosyAnalyser.ApiService.Options;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
    public string ApiVersion { get; set; } = "2023-06-01";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(3);
}
