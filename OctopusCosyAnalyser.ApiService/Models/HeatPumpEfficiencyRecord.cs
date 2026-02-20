namespace OctopusCosyAnalyser.ApiService.Models;

public class HeatPumpEfficiencyRecord
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    // Core measurements
    public decimal ElectricityKWh { get; set; }
    public decimal OutdoorAvgC { get; set; }
    public decimal? OutdoorHighC { get; set; }
    public decimal? OutdoorLowC { get; set; }
    public decimal? IndoorAvgC { get; set; }

    // Comfort
    public int? ComfortScore { get; set; } // 1–5

    // Change tracking
    public bool ChangeActive { get; set; }
    public string? ChangeDescription { get; set; }
    public string? Notes { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
