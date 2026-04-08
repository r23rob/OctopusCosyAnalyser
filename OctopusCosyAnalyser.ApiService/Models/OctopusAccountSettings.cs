namespace OctopusCosyAnalyser.ApiService.Models;

public class OctopusAccountSettings
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? OctopusPassword { get; set; }
    public string? AnthropicApiKey { get; set; }

    /// <summary>
    /// Authentication mode: "apikey" (default) or "password".
    /// </summary>
    public string AuthMode { get; set; } = "apikey";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

