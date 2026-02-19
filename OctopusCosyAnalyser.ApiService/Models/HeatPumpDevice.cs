namespace OctopusCosyAnalyser.ApiService.Models;

public class HeatPumpDevice
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? MeterSerialNumber { get; set; }
    public string? Mpan { get; set; }
    public string? Euid { get; set; }
    public int? PropertyId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
}
