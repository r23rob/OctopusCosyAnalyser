using OctopusCosyAnalyser.ApiService.Services;

namespace OctopusCosyAnalyser.Tests;

public class EfficiencyCalculationServiceTests
{
    [Test]
    public void ComputeHdd_ColdDay_ReturnsPositiveHdd()
    {
        var hdd = EfficiencyCalculationService.ComputeHdd(5m);
        Assert.That(hdd, Is.EqualTo(10.5m));
    }

    [Test]
    public void ComputeHdd_AtBaseTemp_ReturnsZero()
    {
        var hdd = EfficiencyCalculationService.ComputeHdd(15.5m);
        Assert.That(hdd, Is.EqualTo(0m));
    }

    [Test]
    public void ComputeHdd_WarmDay_ReturnsZero()
    {
        var hdd = EfficiencyCalculationService.ComputeHdd(20m);
        Assert.That(hdd, Is.EqualTo(0m));
    }

    [Test]
    public void ComputeHdd_Freezing_ReturnsHighHdd()
    {
        var hdd = EfficiencyCalculationService.ComputeHdd(-5m);
        Assert.That(hdd, Is.EqualTo(20.5m));
    }

    [Test]
    public void ComputeNormalisedEfficiency_PositiveHdd_ReturnsRatio()
    {
        var result = EfficiencyCalculationService.ComputeNormalisedEfficiency(30m, 10m);
        Assert.That(result, Is.EqualTo(3m));
    }

    [Test]
    public void ComputeNormalisedEfficiency_ZeroHdd_ReturnsNull()
    {
        var result = EfficiencyCalculationService.ComputeNormalisedEfficiency(30m, 0m);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ComputeNormalisedEfficiency_SmallHdd_ReturnsHighRatio()
    {
        var result = EfficiencyCalculationService.ComputeNormalisedEfficiency(20m, 0.5m);
        Assert.That(result, Is.EqualTo(40m));
    }
}
