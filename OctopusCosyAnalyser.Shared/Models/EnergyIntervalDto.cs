namespace OctopusCosyAnalyser.Shared.Models;

public sealed class EnergyIntervalDto
{
    public DateTime IntervalStart { get; set; }
    public DateTime IntervalEnd { get; set; }

    // Consumption
    public decimal? ConsumptionKwh { get; set; }
    public decimal? DemandW { get; set; }

    // Heat pump
    public decimal? HeatOutputKwh { get; set; }
    public decimal? AvgCop { get; set; }
    public decimal? AvgPowerInputKw { get; set; }
    public decimal? AvgOutdoorTempC { get; set; }
    public decimal? AvgRoomTempC { get; set; }
    public decimal? AvgFlowTempC { get; set; }
    public bool? WasHeating { get; set; }
    public bool? WasHotWater { get; set; }
    public short SnapshotCount { get; set; }

    // Tariff + cost
    public decimal? UnitRatePencePerKwh { get; set; }
    public decimal? StandingChargePence { get; set; }
    public decimal? CostPence { get; set; }
}
