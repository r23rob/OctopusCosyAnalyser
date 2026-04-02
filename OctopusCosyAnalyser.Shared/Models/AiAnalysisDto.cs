namespace OctopusCosyAnalyser.Shared.Models;

public sealed class AiAnalysisRequestDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? Question { get; set; }
}

public sealed class AiAnalysisResponseDto
{
    public string Analysis { get; set; } = "";
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int DaysAnalysed { get; set; }
    public int TotalSnapshots { get; set; }
}
