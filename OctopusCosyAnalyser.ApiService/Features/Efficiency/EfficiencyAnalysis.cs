using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>
/// Pure analysis functions: period comparison, change-group summaries, and temperature-range filtering.
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
            // Lower kWh/HDD = more efficient
            improved = changeEff < baseEff;
            changePct = baseEff != 0 ? Math.Round((changeEff - baseEff) / baseEff * 100, 2) : null;

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
