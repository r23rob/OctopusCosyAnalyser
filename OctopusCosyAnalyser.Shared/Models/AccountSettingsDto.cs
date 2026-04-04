namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Account settings returned to the frontend — secrets are masked.
/// </summary>
public sealed class AccountSettingsDto
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public string? Email { get; set; }
    public bool HasOctopusPassword { get; set; }
    public bool HasAnthropicApiKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request body for upserting account settings.
/// </summary>
public sealed class AccountSettingsRequestDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string Email { get; set; } = string.Empty;
    public string OctopusPassword { get; set; } = string.Empty;
    public string? AnthropicApiKey { get; set; }
}

