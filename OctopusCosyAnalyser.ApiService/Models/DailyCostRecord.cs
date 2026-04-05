namespace OctopusCosyAnalyser.ApiService.Models;

public class DailyCostRecord
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal TotalCostPence { get; set; }
    public decimal TotalUsageKwh { get; set; }
    public decimal AvgUnitRatePence { get; set; }
    public decimal? StandingChargePence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
