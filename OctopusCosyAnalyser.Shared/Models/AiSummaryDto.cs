namespace OctopusCosyAnalyser.Shared.Models;

public sealed class AiSummaryDto
{
    public string WeekSummary { get; set; } = "";
    public string MonthSummary { get; set; } = "";
    public string YearSummary { get; set; } = "";
    public string Suggestions { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
}
