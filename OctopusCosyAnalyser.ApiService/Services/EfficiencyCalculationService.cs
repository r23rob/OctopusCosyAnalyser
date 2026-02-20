using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

/// <summary>
/// Computes derived metrics (HDD, NormalisedEfficiency) for efficiency records.
/// Base temperature for HDD = 15.5°C.
/// </summary>
public static class EfficiencyCalculationService
{
    public const decimal HddBaseTemperature = 15.5m;

    /// <summary>
    /// Heating Degree Days: max(0, BaseTemp − OutdoorAvgC)
    /// </summary>
    public static decimal ComputeHdd(decimal outdoorAvgC)
        => Math.Max(0, HddBaseTemperature - outdoorAvgC);

    /// <summary>
    /// NormalisedEfficiency = ElectricityKWh / HDD when HDD > 0, otherwise null.
    /// </summary>
    public static decimal? ComputeNormalisedEfficiency(decimal electricityKWh, decimal hdd)
        => hdd > 0 ? electricityKWh / hdd : null;

    /// <summary>
    /// Maps a domain entity to a DTO, computing derived metrics.
    /// </summary>
    public static HeatPumpEfficiencyRecordDto ToDto(HeatPumpEfficiencyRecord record)
    {
        var hdd = ComputeHdd(record.OutdoorAvgC);
        return new HeatPumpEfficiencyRecordDto
        {
            Id = record.Id,
            Date = record.Date,
            ElectricityKWh = record.ElectricityKWh,
            OutdoorAvgC = record.OutdoorAvgC,
            OutdoorHighC = record.OutdoorHighC,
            OutdoorLowC = record.OutdoorLowC,
            IndoorAvgC = record.IndoorAvgC,
            ComfortScore = record.ComfortScore,
            ChangeActive = record.ChangeActive,
            ChangeDescription = record.ChangeDescription,
            Notes = record.Notes,
            HeatingDegreeDays = hdd,
            NormalisedEfficiency = ComputeNormalisedEfficiency(record.ElectricityKWh, hdd),
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }
}
