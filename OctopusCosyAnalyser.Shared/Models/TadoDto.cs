namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Tado account credentials stored in the database.
/// </summary>
public sealed class TadoSettingsDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public long? HomeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request body for upserting Tado settings.
/// </summary>
public sealed class TadoSettingsRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public long? HomeId { get; set; }
}

/// <summary>
/// A Tado home.
/// </summary>
public sealed class TadoHomeDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// A Tado zone with its current temperature.
/// </summary>
public sealed class TadoZoneDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal? CurrentTemperatureCelsius { get; set; }
    public decimal? CurrentHumidityPercentage { get; set; }
    public decimal? SetpointTemperatureCelsius { get; set; }
    public bool? HeatingOn { get; set; }
}
