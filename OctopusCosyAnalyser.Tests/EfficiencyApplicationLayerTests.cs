using OctopusCosyAnalyser.ApiService.Application.Efficiency;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.Tests;

/// <summary>
/// Unit tests for the pure Application-layer domain logic:
/// HddService (heating degree day computation) and EfficiencyAnalysis
/// (comparison, grouping, temperature-range filtering).
/// These tests run in-process with no I/O or database required.
/// </summary>
public class EfficiencyApplicationLayerTests
{
    // ── HddService ────────────────────────────────────────────────────────────

    [Test]
    [TestCase(10.0, 5.5)]      // 15.5 - 10 = 5.5
    [TestCase(15.5, 0.0)]      // exactly at base → 0
    [TestCase(20.0, 0.0)]      // above base → clamped to 0
    [TestCase(-5.0, 20.5)]     // cold day
    public void HddService_ComputeHdd_ReturnsCorrectValue(double outdoor, double expected)
    {
        var hdd = HddService.ComputeHdd((decimal)outdoor);
        Assert.That(hdd, Is.EqualTo((decimal)expected));
    }

    [Test]
    public void HddService_NormalisedEfficiency_ReturnsNullWhenHddIsZero()
    {
        var result = HddService.ComputeNormalisedEfficiency(10m, 0m);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HddService_NormalisedEfficiency_DividesKwhByHdd()
    {
        var result = HddService.ComputeNormalisedEfficiency(20m, 4m);
        Assert.That(result, Is.EqualTo(5m));
    }

    // ── EfficiencyAnalysis.Compare ────────────────────────────────────────────

    [Test]
    public void Compare_WithEmptyLists_ReturnsWarningsAndNoImprovement()
    {
        var result = EfficiencyAnalysis.Compare([], []);

        Assert.That(result.EfficiencyImproved, Is.Null);
        Assert.That(result.Warnings, Is.Not.Empty);
    }

    [Test]
    public void Compare_ChangeHasLowerNormalisedEfficiency_ReturnsImproved()
    {
        var baseline = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(electricityKWh: 10m, outdoorAvgC: 5m),   // HDD=10.5, NE=0.952
            MakeRecord(electricityKWh: 10m, outdoorAvgC: 5m),
            MakeRecord(electricityKWh: 10m, outdoorAvgC: 5m),
        };

        var change = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(electricityKWh: 7m, outdoorAvgC: 5m),    // HDD=10.5, NE=0.667 (better)
            MakeRecord(electricityKWh: 7m, outdoorAvgC: 5m),
            MakeRecord(electricityKWh: 7m, outdoorAvgC: 5m),
        };

        var result = EfficiencyAnalysis.Compare(baseline, change);

        Assert.That(result.EfficiencyImproved, Is.True);
        Assert.That(result.EfficiencyChangePct, Is.LessThan(0));
    }

    [Test]
    public void Compare_ChangeHasHigherNormalisedEfficiency_ReturnsNotImproved()
    {
        var baseline = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(electricityKWh: 7m, outdoorAvgC: 5m),
            MakeRecord(electricityKWh: 7m, outdoorAvgC: 5m),
            MakeRecord(electricityKWh: 7m, outdoorAvgC: 5m),
        };

        var change = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(electricityKWh: 12m, outdoorAvgC: 5m),   // worse
            MakeRecord(electricityKWh: 12m, outdoorAvgC: 5m),
            MakeRecord(electricityKWh: 12m, outdoorAvgC: 5m),
        };

        var result = EfficiencyAnalysis.Compare(baseline, change);

        Assert.That(result.EfficiencyImproved, Is.False);
        Assert.That(result.EfficiencyChangePct, Is.GreaterThan(0));
    }

    // ── EfficiencyAnalysis.GroupByChange ─────────────────────────────────────

    [Test]
    public void GroupByChange_GroupsRecordsByDescription()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(changeDescription: "Tweak A"),
            MakeRecord(changeDescription: "Tweak A"),
            MakeRecord(changeDescription: "Tweak B"),
        };

        var groups = EfficiencyAnalysis.GroupByChange(records);

        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups.Single(g => g.ChangeDescription == "Tweak A").Records, Has.Count.EqualTo(2));
        Assert.That(groups.Single(g => g.ChangeDescription == "Tweak B").Records, Has.Count.EqualTo(1));
    }

    // ── EfficiencyAnalysis.FilterByTemperatureRange ───────────────────────────

    [Test]
    public void FilterByTemperatureRange_ExcludesRecordsOutsideRange()
    {
        var records = new List<HeatPumpEfficiencyRecordDto>
        {
            MakeRecord(outdoorAvgC: -5m),
            MakeRecord(outdoorAvgC: 0m),
            MakeRecord(outdoorAvgC: 5m),
            MakeRecord(outdoorAvgC: 10m),
        };

        var filtered = EfficiencyAnalysis.FilterByTemperatureRange(records, -1m, 6m);

        Assert.That(filtered, Has.Count.EqualTo(2));
        Assert.That(filtered.All(r => r.OutdoorAvgC >= -1m && r.OutdoorAvgC <= 6m), Is.True);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HeatPumpEfficiencyRecordDto MakeRecord(
        decimal electricityKWh = 10m,
        decimal outdoorAvgC = 5m,
        bool changeActive = false,
        string? changeDescription = null)
    {
        var hdd = HddService.ComputeHdd(outdoorAvgC);
        return new HeatPumpEfficiencyRecordDto
        {
            Date = DateOnly.FromDateTime(DateTime.Today),
            ElectricityKWh = electricityKWh,
            OutdoorAvgC = outdoorAvgC,
            ChangeActive = changeActive,
            ChangeDescription = changeDescription,
            HeatingDegreeDays = hdd,
            NormalisedEfficiency = HddService.ComputeNormalisedEfficiency(electricityKWh, hdd),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
