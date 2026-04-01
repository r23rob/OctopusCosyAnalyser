namespace OctopusCosyAnalyser.ApiService.Models;

public class HeatPumpTimeSeriesRecord
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal? EnergyInputKwh { get; set; }
    public decimal? EnergyOutputKwh { get; set; }
    public decimal? OutdoorTemperatureCelsius { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
