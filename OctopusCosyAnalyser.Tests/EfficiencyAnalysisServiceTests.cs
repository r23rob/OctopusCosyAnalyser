using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.Tests;

public class EfficiencyAnalysisServiceTests
{
    private static HeatPumpEfficiencyRecordDto MakeRecord(
        decimal electricityKWh, decimal outdoorAvgC, bool changeActive = false, string? changeDescription = null)
    {
        var hdd = EfficiencyCalculationService.ComputeHdd(outdoorAvgC);
        return new HeatPumpEfficiencyRecordDto
        {
            ElectricityKWh = electricityKWh,
            OutdoorAvgC = outdoorAvgC,
            HeatingDegreeDays = hdd,
            NormalisedEfficiency = EfficiencyCalculationService.ComputeNormalisedEfficiency(electricityKWh, hdd),
            ChangeActive = changeActive,
            ChangeDescription = changeDescription
        };
    }

    [Test]
    public void Summarise_EmptyRecords_ReturnsEmptySummary()
    {
        var result = EfficiencyAnalysisService.Summarise("Test", []);

        Assert.That(result.Label, Is.EqualTo("Test"));
        Assert.That(result.RecordCount, Is.EqualTo(0));
        Assert.That(result.AvgNormalisedEfficiency, Is.Null);
    }

    [Test]
    public void Summarise_WithAnalysableRecords_ComputesAverages()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(20m, 5m),   // HDD=10.5, NormEff=20/10.5
            MakeRecord(30m, 0m),   // HDD=15.5, NormEff=30/15.5
            MakeRecord(25m, 10m),  // HDD=5.5, NormEff=25/5.5
        };

        var result = EfficiencyAnalysisService.Summarise("Cold days", records);

        Assert.That(result.RecordCount, Is.EqualTo(3));
        Assert.That(result.AnalysableRecords, Is.EqualTo(3));
        Assert.That(result.AvgElectricityKWh, Is.EqualTo(25m));
        Assert.That(result.AvgNormalisedEfficiency, Is.Not.Null);
    }

    [Test]
    public void Summarise_WarmDaysExcluded_FromNormalisedEfficiency()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(20m, 5m),   // HDD=10.5, analysable
            MakeRecord(10m, 20m),  // HDD=0, warm day — excluded from normalised
        };

        var result = EfficiencyAnalysisService.Summarise("Mixed", records);

        Assert.That(result.RecordCount, Is.EqualTo(2));
        Assert.That(result.AnalysableRecords, Is.EqualTo(1));
        // Avg normalised efficiency only from the cold day
        Assert.That(result.AvgNormalisedEfficiency, Is.EqualTo(20m / 10.5m).Within(0.01m));
    }

    [Test]
    public void Compare_EfficiencyImproved_WhenChangeLowerKwhPerHdd()
    {
        // Baseline: 30 kWh at 5°C (HDD=10.5) → 2.857 kWh/HDD
        var baseline = Enumerable.Range(0, 5).Select(_ => MakeRecord(30m, 5m)).ToList();
        // Change: 20 kWh at 5°C (HDD=10.5) → 1.905 kWh/HDD (better)
        var change = Enumerable.Range(0, 5).Select(_ => MakeRecord(20m, 5m)).ToList();

        var result = EfficiencyAnalysisService.Compare(baseline, change);

        Assert.That(result.EfficiencyImproved, Is.True);
        Assert.That(result.EfficiencyChangePct, Is.LessThan(0));
    }

    [Test]
    public void Compare_EfficiencyWorsened_WhenChangeHigherKwhPerHdd()
    {
        var baseline = Enumerable.Range(0, 5).Select(_ => MakeRecord(20m, 5m)).ToList();
        var change = Enumerable.Range(0, 5).Select(_ => MakeRecord(30m, 5m)).ToList();

        var result = EfficiencyAnalysisService.Compare(baseline, change);

        Assert.That(result.EfficiencyImproved, Is.False);
        Assert.That(result.EfficiencyChangePct, Is.GreaterThan(0));
    }

    [Test]
    public void Compare_WarnsWhenFewAnalysableDays()
    {
        var baseline = new List<HeatPumpEfficiencyRecordDto> { MakeRecord(20m, 5m) };
        var change = new List<HeatPumpEfficiencyRecordDto> { MakeRecord(20m, 5m) };

        var result = EfficiencyAnalysisService.Compare(baseline, change);

        Assert.That(result.Warnings, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(result.Warnings, Has.Some.Contains("fewer than 3"));
    }

    [Test]
    public void Compare_WarnsWhenLargeTemperatureDifference()
    {
        var baseline = Enumerable.Range(0, 5).Select(_ => MakeRecord(30m, 0m)).ToList();
        var change = Enumerable.Range(0, 5).Select(_ => MakeRecord(25m, 10m)).ToList();

        var result = EfficiencyAnalysisService.Compare(baseline, change);

        Assert.That(result.Warnings, Has.Some.Contains("outdoor temperature differs"));
    }

    [Test]
    public void GroupByChange_GroupsCorrectly()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(20m, 5m, changeDescription: "Lowered WC curve"),
            MakeRecord(22m, 4m, changeDescription: "Lowered WC curve"),
            MakeRecord(25m, 3m, changeDescription: "Increased insulation"),
        };

        var groups = EfficiencyAnalysisService.GroupByChange(records);

        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups.Select(g => g.ChangeDescription), Does.Contain("Lowered WC curve"));
        Assert.That(groups.Select(g => g.ChangeDescription), Does.Contain("Increased insulation"));
        Assert.That(groups.First(g => g.ChangeDescription == "Lowered WC curve").Records, Has.Count.EqualTo(2));
    }

    [Test]
    public void GroupByChange_NullDescription_GroupedAsNoChange()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(20m, 5m, changeDescription: null),
            MakeRecord(22m, 4m, changeDescription: null),
        };

        var groups = EfficiencyAnalysisService.GroupByChange(records);

        Assert.That(groups, Has.Count.EqualTo(1));
        Assert.That(groups[0].ChangeDescription, Is.EqualTo("(no change)"));
    }

    [Test]
    public void FilterByTemperatureRange_FiltersCorrectly()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(20m, -2m),
            MakeRecord(25m, 5m),
            MakeRecord(30m, 12m),
            MakeRecord(10m, 18m),
        };

        var filtered = EfficiencyAnalysisService.FilterByTemperatureRange(records, 0m, 15m);

        Assert.That(filtered, Has.Count.EqualTo(2));
        Assert.That(filtered.All(r => r.OutdoorAvgC >= 0m && r.OutdoorAvgC <= 15m), Is.True);
    }
}
