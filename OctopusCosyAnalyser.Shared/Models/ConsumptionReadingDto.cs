namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Electricity consumption reading from smart meter.
/// </summary>
public sealed class ConsumptionReadingDto
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ReadAt { get; set; }
    public decimal Consumption { get; set; }
    public decimal? ConsumptionDelta { get; set; }
    public decimal? Demand { get; set; }
    public DateTime CreatedAt { get; set; }
}

