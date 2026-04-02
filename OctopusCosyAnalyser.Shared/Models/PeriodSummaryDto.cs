namespace OctopusCosyAnalyser.Shared.Models;

public sealed class PeriodSummaryDto
{
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public int SnapshotCount { get; set; }

    // COP
    public double? AvgCop { get; set; }
    public double? MinCop { get; set; }
    public double? MaxCop { get; set; }

    // Energy totals (kWh) — sum of (kW × 0.25h per snapshot interval)
    public double TotalInputKwh { get; set; }
    public double TotalOutputKwh { get; set; }

    // Outdoor temperature
    public double? AvgOutdoorTemp { get; set; }
    public double? MinOutdoorTemp { get; set; }
    public double? MaxOutdoorTemp { get; set; }

    // Room temperature (Cosy Pod)
    public double? AvgRoomTemp { get; set; }
    public double? MinRoomTemp { get; set; }
    public double? MaxRoomTemp { get; set; }

    // Room humidity (Cosy Pod)
    public double? AvgRoomHumidity { get; set; }
    public double? MinRoomHumidity { get; set; }
    public double? MaxRoomHumidity { get; set; }

    // Hot water setpoint
    public double? AvgHotWaterSetpoint { get; set; }
    public double? MinHotWaterSetpoint { get; set; }
    public double? MaxHotWaterSetpoint { get; set; }

    // Flow temperature
    public double? AvgFlowTemp { get; set; }
    public double? MinFlowTemp { get; set; }
    public double? MaxFlowTemp { get; set; }

    // Duty cycles
    public double HeatingDutyCyclePercent { get; set; }
    public double HotWaterDutyCyclePercent { get; set; }
}
