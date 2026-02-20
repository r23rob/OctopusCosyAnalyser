namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// A single daily efficiency record used for heat pump performance tracking.
/// Includes derived metrics (HDD and NormalisedEfficiency) computed server-side.
/// </summary>
public sealed class HeatPumpEfficiencyRecordDto
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    // Core measurements
    public decimal ElectricityKWh { get; set; }
    public decimal OutdoorAvgC { get; set; }
    public decimal? OutdoorHighC { get; set; }
    public decimal? OutdoorLowC { get; set; }
    public decimal? IndoorAvgC { get; set; }

    // Comfort
    public int? ComfortScore { get; set; }

    // Change tracking
    public bool ChangeActive { get; set; }
    public string? ChangeDescription { get; set; }
    public string? Notes { get; set; }

    // Derived metrics
    public decimal HeatingDegreeDays { get; set; }
    public decimal? NormalisedEfficiency { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request DTO for creating or updating an efficiency record.
/// </summary>
public sealed class HeatPumpEfficiencyRecordRequest
{
    public DateOnly Date { get; set; }
    public decimal ElectricityKWh { get; set; }
    public decimal OutdoorAvgC { get; set; }
    public decimal? OutdoorHighC { get; set; }
    public decimal? OutdoorLowC { get; set; }
    public decimal? IndoorAvgC { get; set; }
    public int? ComfortScore { get; set; }
    public bool ChangeActive { get; set; }
    public string? ChangeDescription { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Summary statistics for a group of efficiency records (e.g. baseline or change period).
/// </summary>
public sealed class EfficiencyPeriodSummaryDto
{
    public string Label { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public decimal AvgElectricityKWh { get; set; }
    public decimal AvgOutdoorAvgC { get; set; }
    public decimal AvgHDD { get; set; }
    public decimal? AvgNormalisedEfficiency { get; set; }
    public int AnalysableRecords { get; set; } // Records with HDD > 0
}

/// <summary>
/// Result of a before-vs-after efficiency comparison.
/// </summary>
public sealed class EfficiencyComparisonDto
{
    public EfficiencyPeriodSummaryDto Baseline { get; set; } = new();
    public EfficiencyPeriodSummaryDto Change { get; set; } = new();
    public bool? EfficiencyImproved { get; set; }
    public decimal? EfficiencyChangePct { get; set; }
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Result of grouping records by ChangeDescription.
/// </summary>
public sealed class EfficiencyGroupDto
{
    public string ChangeDescription { get; set; } = string.Empty;
    public EfficiencyPeriodSummaryDto Summary { get; set; } = new();
    public List<HeatPumpEfficiencyRecordDto> Records { get; set; } = [];
}
