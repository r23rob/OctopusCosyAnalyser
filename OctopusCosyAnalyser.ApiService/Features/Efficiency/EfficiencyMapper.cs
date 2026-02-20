using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>
/// Maps between EF entities and DTOs. Computes derived metrics (HDD, NormalisedEfficiency) on the way out.
/// </summary>
internal static class EfficiencyMapper
{
    internal static HeatPumpEfficiencyRecordDto ToDto(HeatPumpEfficiencyRecord record)
    {
        var hdd = HddService.ComputeHdd(record.OutdoorAvgC);
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
            NormalisedEfficiency = HddService.ComputeNormalisedEfficiency(record.ElectricityKWh, hdd),
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }

    internal static HeatPumpEfficiencyRecord ToEntity(HeatPumpEfficiencyRecordRequest request)
    {
        var now = DateTime.UtcNow;
        return new HeatPumpEfficiencyRecord
        {
            Date = request.Date,
            ElectricityKWh = request.ElectricityKWh,
            OutdoorAvgC = request.OutdoorAvgC,
            OutdoorHighC = request.OutdoorHighC,
            OutdoorLowC = request.OutdoorLowC,
            IndoorAvgC = request.IndoorAvgC,
            ComfortScore = request.ComfortScore,
            ChangeActive = request.ChangeActive,
            ChangeDescription = request.ChangeDescription,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    internal static void ApplyUpdate(HeatPumpEfficiencyRecord record, HeatPumpEfficiencyRecordRequest request)
    {
        record.Date = request.Date;
        record.ElectricityKWh = request.ElectricityKWh;
        record.OutdoorAvgC = request.OutdoorAvgC;
        record.OutdoorHighC = request.OutdoorHighC;
        record.OutdoorLowC = request.OutdoorLowC;
        record.IndoorAvgC = request.IndoorAvgC;
        record.ComfortScore = request.ComfortScore;
        record.ChangeActive = request.ChangeActive;
        record.ChangeDescription = request.ChangeDescription;
        record.Notes = request.Notes;
        record.UpdatedAt = DateTime.UtcNow;
    }
}
