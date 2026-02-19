namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// A single point in a time-series performance chart.
/// Parsed from octoHeatPumpTimeSeriesPerformance array items.
/// </summary>
public sealed class TimeSeriesPointDto
{
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? CoefficientOfPerformance { get; set; }
    public string? EnergyOutput { get; set; }
    public string? EnergyInput { get; set; }
    public string? OutdoorTemperature { get; set; }
}

/// <summary>
/// Wrapper returned by /api/heatpump/time-series/{accountNumber}/{euid}.
/// </summary>
public sealed class TimeSeriesResponseDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string Euid { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Grouping { get; set; } = "auto";
    public List<TimeSeriesPointDto> Points { get; set; } = [];
}

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

