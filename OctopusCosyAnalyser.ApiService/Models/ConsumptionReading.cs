namespace OctopusCosyAnalyser.ApiService.Models;

public class ConsumptionReading
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
    public decimal Consumption { get; set; } // kWh
    public decimal? ConsumptionDelta { get; set; } // kWh change
    public decimal? Demand { get; set; } // Watts (negative = export)
    public DateTime CreatedAt { get; set; }
}
