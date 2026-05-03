namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Wrapper returned by /api/heatpump/time-ranged/{accountNumber}/{euid}.
/// </summary>
public sealed class TimeRangedResponseDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Euid { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? CoefficientOfPerformance { get; set; }
    public string? EnergyOutput { get; set; }
    public string? EnergyInput { get; set; }
}
