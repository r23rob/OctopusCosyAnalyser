namespace OctopusCosyAnalyser.ApiService.Models;

public class DailyCostRecord
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public double TotalCostPence { get; set; }
    public double TotalUsageKwh { get; set; }
    public double AvgUnitRatePence { get; set; }
    public double? StandingChargePence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
