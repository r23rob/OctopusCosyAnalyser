namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Account settings used for API authentication.
/// </summary>
public sealed class AccountSettingsDto
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request body for upserting account settings.
/// </summary>
public sealed class AccountSettingsRequestDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

