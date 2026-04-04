using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IHeatPumpAiService
{
    Task<AiSummaryDto> GenerateSummaryAsync(string deviceId, bool forceRefresh = false);
}
