namespace OctopusCosyAnalyser.Shared.Models;

public sealed class EnergySummaryDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int IntervalCount { get; set; }

    // Aggregates
    public decimal? TotalConsumptionKwh { get; set; }
    public decimal? TotalHeatOutputKwh { get; set; }
    public decimal? TotalCostPence { get; set; }
    public decimal? AvgCop { get; set; }
    public decimal? AvgOutdoorTempC { get; set; }
    public decimal? AvgUnitRatePence { get; set; }
    public decimal? TotalStandingChargePence { get; set; }
    public decimal? HeatingDutyCyclePercent { get; set; }
    public decimal? HotWaterDutyCyclePercent { get; set; }
}

public sealed class EnergySummaryResponseDto
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string Grouping { get; set; } = string.Empty;
    public List<EnergySummaryDto> Periods { get; set; } = [];
}
