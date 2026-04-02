namespace OctopusCosyAnalyser.Shared.Models;

public sealed class DailyAggregateDto
{
    public DateOnly Date { get; set; }
    public int SnapshotCount { get; set; }

    // COP averages split by demand type
    public double? AvgCopHeating { get; set; }
    public double? AvgCopHotWater { get; set; }
    public double? AvgCopSpaceHeatingOnly { get; set; }

    // Energy totals (kWh)
    public double TotalElectricityKwh { get; set; }
    public double TotalHeatOutputKwh { get; set; }

    // Temperature stats
    public double? AvgOutdoorTemp { get; set; }
    public double? MinOutdoorTemp { get; set; }
    public double? MaxOutdoorTemp { get; set; }
    public double? AvgFlowTemp { get; set; }
    public double? AvgRoomTemp { get; set; }
    public double? AvgSetpoint { get; set; }

    // Duty cycles (percentage)
    public double HeatingDutyCyclePercent { get; set; }
    public double HotWaterDutyCyclePercent { get; set; }

    // Weather compensation settings (mode for the day)
    public double? WeatherCompMin { get; set; }
    public double? WeatherCompMax { get; set; }

    // Cycling (controller state transitions)
    public int ControllerStateTransitions { get; set; }

    // Cost data (from Octopus costOfUsage API)
    public double? DailyCostPence { get; set; }
    public double? DailyUsageKwh { get; set; }
    public double? AvgUnitRatePence { get; set; }
    public double? CostPerKwhHeatPence { get; set; }

    // Hot water schedule
    public int HotWaterRunCount { get; set; }
    public int HotWaterTotalMinutes { get; set; }
    public double? AvgHotWaterSetpoint { get; set; }
}
