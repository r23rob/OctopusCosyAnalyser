namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>
/// Pure domain logic for heating degree days and normalised efficiency.
/// Base temperature = 15.5°C.
/// </summary>
public static class HddService
{
    public const decimal BaseTemperature = 15.5m;

    public static decimal ComputeHdd(decimal outdoorAvgC)
        => Math.Max(0, BaseTemperature - outdoorAvgC);

    public static decimal? ComputeNormalisedEfficiency(decimal electricityKWh, decimal hdd)
        => hdd > 0 ? electricityKWh / hdd : null;
}
