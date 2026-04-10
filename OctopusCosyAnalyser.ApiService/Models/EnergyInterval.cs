namespace OctopusCosyAnalyser.ApiService.Models;

public class EnergyInterval
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime IntervalStart { get; set; }
    public DateTime IntervalEnd { get; set; }

    // Consumption (from ConsumptionReading)
    public decimal? ConsumptionKwh { get; set; }
    public decimal? DemandW { get; set; }

    // Heat pump (averaged from ≤2 HeatPumpSnapshot rows in window)
    public decimal? HeatOutputKwh { get; set; }
    public decimal? AvgCop { get; set; }
    public decimal? AvgPowerInputKw { get; set; }
    public decimal? AvgOutdoorTempC { get; set; }
    public decimal? AvgRoomTempC { get; set; }
    public decimal? AvgFlowTempC { get; set; }
    public bool? WasHeating { get; set; }
    public bool? WasHotWater { get; set; }
    public short SnapshotCount { get; set; }

    // Tariff (from TariffRates lookup)
    public decimal? UnitRatePencePerKwh { get; set; }
    public decimal? StandingChargePence { get; set; }

    // Derived
    public decimal? CostPence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
