using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Application.Efficiency;

// ── Domain service ────────────────────────────────────────────────────────────

/// <summary>
/// Pure domain logic for computing heating degree days and normalised efficiency.
/// Base temperature for HDD = 15.5°C.
/// </summary>
public static class HddService
{
    public const decimal BaseTemperature = 15.5m;

    public static decimal ComputeHdd(decimal outdoorAvgC)
        => Math.Max(0, BaseTemperature - outdoorAvgC);

    public static decimal? ComputeNormalisedEfficiency(decimal electricityKWh, decimal hdd)
        => hdd > 0 ? electricityKWh / hdd : null;
}

// ── Mapping helper ────────────────────────────────────────────────────────────

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

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetEfficiencyRecordsQuery(DateOnly? From, DateOnly? To);

public class GetEfficiencyRecordsHandler(IEfficiencyRepository repo)
{
    public async Task<List<HeatPumpEfficiencyRecordDto>> HandleAsync(GetEfficiencyRecordsQuery query, CancellationToken ct = default)
    {
        var records = await repo.GetRecordsAsync(query.From, query.To, ct);
        return records.Select(EfficiencyMapper.ToDto).ToList();
    }
}

public record GetEfficiencyRecordQuery(int Id);

public class GetEfficiencyRecordHandler(IEfficiencyRepository repo)
{
    public async Task<HeatPumpEfficiencyRecordDto?> HandleAsync(GetEfficiencyRecordQuery query, CancellationToken ct = default)
    {
        var record = await repo.GetByIdAsync(query.Id, ct);
        return record is null ? null : EfficiencyMapper.ToDto(record);
    }
}

public record ComparePeriodQuery(DateOnly? From, DateOnly? To);

public class ComparePeriodHandler(IEfficiencyRepository repo)
{
    public async Task<EfficiencyComparisonDto> HandleAsync(ComparePeriodQuery query, CancellationToken ct = default)
    {
        var records = await repo.GetRecordsAsync(query.From, query.To, ct);
        var dtos = records.Select(EfficiencyMapper.ToDto).ToList();

        var baseline = dtos.Where(r => !r.ChangeActive).ToList();
        var change = dtos.Where(r => r.ChangeActive).ToList();

        return EfficiencyAnalysis.Compare(baseline, change);
    }
}

public record GetEfficiencyGroupsQuery(DateOnly? From, DateOnly? To);

public class GetEfficiencyGroupsHandler(IEfficiencyRepository repo)
{
    public async Task<List<EfficiencyGroupDto>> HandleAsync(GetEfficiencyGroupsQuery query, CancellationToken ct = default)
    {
        var records = await repo.GetRecordsAsync(query.From, query.To, ct);
        var dtos = records.Select(EfficiencyMapper.ToDto).ToList();
        return EfficiencyAnalysis.GroupByChange(dtos);
    }
}

public record FilterEfficiencyByTempQuery(decimal MinOutdoorC, decimal MaxOutdoorC, DateOnly? From, DateOnly? To);

public class FilterEfficiencyByTempHandler(IEfficiencyRepository repo)
{
    public async Task<List<HeatPumpEfficiencyRecordDto>> HandleAsync(FilterEfficiencyByTempQuery query, CancellationToken ct = default)
    {
        var records = await repo.GetRecordsAsync(query.From, query.To, ct);
        var dtos = records.Select(EfficiencyMapper.ToDto).ToList();
        return EfficiencyAnalysis.FilterByTemperatureRange(dtos, query.MinOutdoorC, query.MaxOutdoorC);
    }
}

// ── Commands ──────────────────────────────────────────────────────────────────

public record CreateEfficiencyRecordCommand(HeatPumpEfficiencyRecordRequest Request);

public sealed class CreateEfficiencyRecordResult
{
    public HeatPumpEfficiencyRecordDto? Record { get; init; }
    public bool Conflict { get; init; }
}

public class CreateEfficiencyRecordHandler(IEfficiencyRepository repo)
{
    public async Task<CreateEfficiencyRecordResult> HandleAsync(CreateEfficiencyRecordCommand command, CancellationToken ct = default)
    {
        if (await repo.ExistsForDateAsync(command.Request.Date, ct: ct))
            return new CreateEfficiencyRecordResult { Conflict = true };

        var record = EfficiencyMapper.ToEntity(command.Request);
        await repo.AddAsync(record, ct);
        await repo.SaveChangesAsync(ct);
        return new CreateEfficiencyRecordResult { Record = EfficiencyMapper.ToDto(record) };
    }
}

public record UpdateEfficiencyRecordCommand(int Id, HeatPumpEfficiencyRecordRequest Request);

public sealed class UpdateEfficiencyRecordResult
{
    public HeatPumpEfficiencyRecordDto? Record { get; init; }
    public bool NotFound { get; init; }
    public bool Conflict { get; init; }
}

public class UpdateEfficiencyRecordHandler(IEfficiencyRepository repo)
{
    public async Task<UpdateEfficiencyRecordResult> HandleAsync(UpdateEfficiencyRecordCommand command, CancellationToken ct = default)
    {
        var record = await repo.GetByIdAsync(command.Id, ct);
        if (record is null)
            return new UpdateEfficiencyRecordResult { NotFound = true };

        if (record.Date != command.Request.Date && await repo.ExistsForDateAsync(command.Request.Date, excludeId: command.Id, ct: ct))
            return new UpdateEfficiencyRecordResult { Conflict = true };

        EfficiencyMapper.ApplyUpdate(record, command.Request);
        await repo.SaveChangesAsync(ct);
        return new UpdateEfficiencyRecordResult { Record = EfficiencyMapper.ToDto(record) };
    }
}

public record DeleteEfficiencyRecordCommand(int Id);

public sealed class DeleteEfficiencyRecordResult
{
    public bool NotFound { get; init; }
    public bool Deleted { get; init; }
}

public class DeleteEfficiencyRecordHandler(IEfficiencyRepository repo)
{
    public async Task<DeleteEfficiencyRecordResult> HandleAsync(DeleteEfficiencyRecordCommand command, CancellationToken ct = default)
    {
        var record = await repo.GetByIdAsync(command.Id, ct);
        if (record is null)
            return new DeleteEfficiencyRecordResult { NotFound = true };

        await repo.DeleteAsync(record, ct);
        await repo.SaveChangesAsync(ct);
        return new DeleteEfficiencyRecordResult { Deleted = true };
    }
}

// ── Analysis (domain logic, no I/O) ──────────────────────────────────────────

/// <summary>
/// Pure analysis helpers: comparing efficiency periods, grouping, and temperature-range filtering.
/// Days with HDD = 0 are excluded from NormalisedEfficiency averages.
/// </summary>
public static class EfficiencyAnalysis
{
    public static EfficiencyPeriodSummaryDto Summarise(string label, IReadOnlyList<HeatPumpEfficiencyRecordDto> records)
    {
        if (records.Count == 0)
            return new EfficiencyPeriodSummaryDto { Label = label };

        var analysable = records.Where(r => r.HeatingDegreeDays > 0).ToList();

        return new EfficiencyPeriodSummaryDto
        {
            Label = label,
            RecordCount = records.Count,
            AvgElectricityKWh = records.Average(r => r.ElectricityKWh),
            AvgOutdoorAvgC = records.Average(r => r.OutdoorAvgC),
            AvgHDD = records.Average(r => r.HeatingDegreeDays),
            AvgNormalisedEfficiency = analysable.Count > 0
                ? analysable.Average(r => r.NormalisedEfficiency!.Value)
                : null,
            AnalysableRecords = analysable.Count
        };
    }

    public static EfficiencyComparisonDto Compare(
        IReadOnlyList<HeatPumpEfficiencyRecordDto> baseline,
        IReadOnlyList<HeatPumpEfficiencyRecordDto> change)
    {
        var baselineSummary = Summarise("Baseline", baseline);
        var changeSummary = Summarise("Change Period", change);

        var warnings = new List<string>();
        bool? improved = null;
        decimal? changePct = null;

        if (baselineSummary.AnalysableRecords < 3)
            warnings.Add("Baseline has fewer than 3 analysable days (HDD > 0). Results may be unreliable.");

        if (changeSummary.AnalysableRecords < 3)
            warnings.Add("Change period has fewer than 3 analysable days (HDD > 0). Results may be unreliable.");

        if (baselineSummary.AvgNormalisedEfficiency.HasValue && changeSummary.AvgNormalisedEfficiency.HasValue)
        {
            var baseEff = baselineSummary.AvgNormalisedEfficiency.Value;
            var changeEff = changeSummary.AvgNormalisedEfficiency.Value;

            // Lower NormalisedEfficiency (kWh/HDD) means less electricity per degree of heating = more efficient
            improved = changeEff < baseEff;
            changePct = baseEff != 0
                ? Math.Round((changeEff - baseEff) / baseEff * 100, 2)
                : null;

            var outdoorDiff = Math.Abs(baselineSummary.AvgOutdoorAvgC - changeSummary.AvgOutdoorAvgC);
            if (outdoorDiff > 3)
                warnings.Add($"Average outdoor temperature differs by {outdoorDiff:F1}°C between periods. HDD normalisation may not fully compensate.");
        }
        else
        {
            warnings.Add("Insufficient data to compare efficiency between periods.");
        }

        return new EfficiencyComparisonDto
        {
            Baseline = baselineSummary,
            Change = changeSummary,
            EfficiencyImproved = improved,
            EfficiencyChangePct = changePct,
            Warnings = warnings
        };
    }

    public static List<EfficiencyGroupDto> GroupByChange(IReadOnlyList<HeatPumpEfficiencyRecordDto> records)
        => records
            .GroupBy(r => r.ChangeDescription ?? "(no change)")
            .Select(g => new EfficiencyGroupDto
            {
                ChangeDescription = g.Key,
                Summary = Summarise(g.Key, g.ToList()),
                Records = g.ToList()
            })
            .ToList();

    public static List<HeatPumpEfficiencyRecordDto> FilterByTemperatureRange(
        IReadOnlyList<HeatPumpEfficiencyRecordDto> records,
        decimal minOutdoorC,
        decimal maxOutdoorC)
        => records
            .Where(r => r.OutdoorAvgC >= minOutdoorC && r.OutdoorAvgC <= maxOutdoorC)
            .ToList();
}
