using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IAiAnalysisService
{
    Task<string> AnalyseAsync(List<DailyAggregateDto> aggregates, string? userQuestion, string? anthropicApiKey = null, CancellationToken ct = default);
    Task<AiSummaryDto> GenerateDashboardSummaryAsync(string deviceId, bool forceRefresh = false, string? anthropicApiKey = null);
}
