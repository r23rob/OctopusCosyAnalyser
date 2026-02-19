namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Registered heat pump device returned by /api/heatpump/devices.
/// </summary>
public sealed class HeatPumpDeviceDto
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? MeterSerialNumber { get; set; }
    public string? Mpan { get; set; }
    public string? Euid { get; set; }
    public int? PropertyId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
}

/// <summary>
/// Result from /api/heatpump/setup.
/// </summary>
public sealed class SetupResponseDto
{
    public string? DeviceId { get; set; }
    public string? Mpan { get; set; }
    public string? SerialNumber { get; set; }
    public string? Euid { get; set; }
    public int? PropertyId { get; set; }
    public string? Message { get; set; }
}

