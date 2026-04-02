using System.Collections.Concurrent;
using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public class HeatPumpAiService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HeatPumpAiService> _logger;
    private readonly ConcurrentDictionary<string, (AiSummaryDto Summary, DateTime CachedAt)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public HeatPumpAiService(IServiceScopeFactory scopeFactory, ILogger<HeatPumpAiService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<AiSummaryDto> GenerateSummaryAsync(string deviceId, bool forceRefresh = false)
    {
        if (!forceRefresh && _cache.TryGetValue(deviceId, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheDuration)
            return cached.Summary;

        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            return new AiSummaryDto
            {
                WeekSummary = "AI summaries are not available. Set the ANTHROPIC_API_KEY environment variable to enable this feature.",
                MonthSummary = "",
                YearSummary = "",
                Suggestions = "",
                GeneratedAt = DateTime.UtcNow
            };
        }

        try
        {
            var prompt = await BuildPromptAsync(deviceId);
            var client = new AnthropicClient();

            var parameters = new MessageCreateParams
            {
                MaxTokens = 1500,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = prompt
                    }
                ],
                Model = "claude-sonnet-4-6"
            };

            var message = await client.Messages.Create(parameters);
            var responseText = message.Content?.FirstOrDefault()?.ToString() ?? "";

            var summary = ParseResponse(responseText);
            summary.GeneratedAt = DateTime.UtcNow;

            _cache[deviceId] = (summary, DateTime.UtcNow);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI summary for device {DeviceId}", deviceId);
            return new AiSummaryDto
            {
                WeekSummary = $"Failed to generate AI summary: {ex.Message}",
                MonthSummary = "",
                YearSummary = "",
                Suggestions = "",
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<string> BuildPromptAsync(string deviceId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();

        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);
        var yearAgo = now.AddDays(-365);

        var weekStats = await GetPeriodStatsAsync(db, deviceId, weekAgo, now);
        var monthStats = await GetPeriodStatsAsync(db, deviceId, monthAgo, now);
        var yearStats = await GetPeriodStatsAsync(db, deviceId, yearAgo, now);

        var sb = new StringBuilder();
        sb.AppendLine("You are an expert heat pump analyst. Analyse the following heat pump performance data and provide a concise, helpful summary.");
        sb.AppendLine("The user is a UK homeowner monitoring their Octopus Energy Cosy heat pump.");
        sb.AppendLine("A COP (Coefficient of Performance) above 3.0 is good, above 3.5 is excellent, below 2.5 is poor.");
        sb.AppendLine("Weather compensation (WC) adjusts flow temperature based on outdoor conditions — lower flow temps = higher efficiency.");
        sb.AppendLine();
        sb.AppendLine("=== LAST 7 DAYS ===");
        AppendStats(sb, weekStats);
        sb.AppendLine();
        sb.AppendLine("=== LAST 30 DAYS ===");
        AppendStats(sb, monthStats);
        sb.AppendLine();
        sb.AppendLine("=== LAST 365 DAYS ===");
        AppendStats(sb, yearStats);
        sb.AppendLine();
        sb.AppendLine("Respond in EXACTLY this format with 4 sections. Each section should be 2-3 sentences max. Be specific with numbers. Use plain English.");
        sb.AppendLine();
        sb.AppendLine("[WEEK]");
        sb.AppendLine("Your summary of the last 7 days performance here.");
        sb.AppendLine();
        sb.AppendLine("[MONTH]");
        sb.AppendLine("Your summary of the last 30 days performance here.");
        sb.AppendLine();
        sb.AppendLine("[YEAR]");
        sb.AppendLine("Your summary of the last 365 days performance here.");
        sb.AppendLine();
        sb.AppendLine("[SUGGESTIONS]");
        sb.AppendLine("Your specific, actionable suggestions for improving efficiency here. If performance is already good, say so.");

        return sb.ToString();
    }

    private static async Task<PeriodStats> GetPeriodStatsAsync(CosyDbContext db, string deviceId, DateTime from, DateTime to)
    {
        var snapshots = await db.HeatPumpSnapshots
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
            .ToListAsync();

        if (snapshots.Count == 0)
            return new PeriodStats { SnapshotCount = 0 };

        var withCop = snapshots.Where(s => s.CoefficientOfPerformance.HasValue).ToList();
        var withOutdoor = snapshots.Where(s => s.OutdoorTemperatureCelsius.HasValue).ToList();
        var withPower = snapshots.Where(s => s.PowerInputKilowatt.HasValue).ToList();
        var withHeat = snapshots.Where(s => s.HeatOutputKilowatt.HasValue).ToList();
        var withFlow = snapshots.Where(s => s.HeatingFlowTemperatureCelsius.HasValue).ToList();
        var withRoom = snapshots.Where(s => s.RoomTemperatureCelsius.HasValue).ToList();
        var withSetpoint = snapshots.Where(s => s.HeatingZoneSetpointCelsius.HasValue).ToList();
        var withDemand = snapshots.Where(s => s.HeatingZoneHeatDemand.HasValue).ToList();

        return new PeriodStats
        {
            SnapshotCount = snapshots.Count,
            AvgCop = withCop.Count > 0 ? (double)withCop.Average(s => s.CoefficientOfPerformance!.Value) : null,
            MinCop = withCop.Count > 0 ? (double)withCop.Min(s => s.CoefficientOfPerformance!.Value) : null,
            MaxCop = withCop.Count > 0 ? (double)withCop.Max(s => s.CoefficientOfPerformance!.Value) : null,
            AvgOutdoorTemp = withOutdoor.Count > 0 ? (double)withOutdoor.Average(s => s.OutdoorTemperatureCelsius!.Value) : null,
            MinOutdoorTemp = withOutdoor.Count > 0 ? (double)withOutdoor.Min(s => s.OutdoorTemperatureCelsius!.Value) : null,
            MaxOutdoorTemp = withOutdoor.Count > 0 ? (double)withOutdoor.Max(s => s.OutdoorTemperatureCelsius!.Value) : null,
            TotalEnergyInKwh = withPower.Count > 0 ? withPower.Sum(s => (double)s.PowerInputKilowatt!.Value) * 0.25 : null,
            TotalHeatOutKwh = withHeat.Count > 0 ? withHeat.Sum(s => (double)s.HeatOutputKilowatt!.Value) * 0.25 : null,
            AvgFlowTemp = withFlow.Count > 0 ? (double)withFlow.Average(s => s.HeatingFlowTemperatureCelsius!.Value) : null,
            AvgRoomTemp = withRoom.Count > 0 ? (double)withRoom.Average(s => s.RoomTemperatureCelsius!.Value) : null,
            AvgSetpoint = withSetpoint.Count > 0 ? (double)withSetpoint.Average(s => s.HeatingZoneSetpointCelsius!.Value) : null,
            DutyCyclePercent = withDemand.Count > 0 ? withDemand.Count(s => s.HeatingZoneHeatDemand == true) * 100.0 / withDemand.Count : null,
            WeatherCompEnabled = snapshots.LastOrDefault()?.WeatherCompensationEnabled,
            WcMin = snapshots.LastOrDefault()?.WeatherCompensationMinCelsius.HasValue == true ? (double)snapshots.Last().WeatherCompensationMinCelsius!.Value : null,
            WcMax = snapshots.LastOrDefault()?.WeatherCompensationMaxCelsius.HasValue == true ? (double)snapshots.Last().WeatherCompensationMaxCelsius!.Value : null,
        };
    }

    private static void AppendStats(StringBuilder sb, PeriodStats stats)
    {
        if (stats.SnapshotCount == 0) { sb.AppendLine("No data available for this period."); return; }

        sb.AppendLine($"Snapshots: {stats.SnapshotCount}");
        if (stats.AvgCop.HasValue) sb.AppendLine($"COP: avg {stats.AvgCop:F2}, min {stats.MinCop:F2}, max {stats.MaxCop:F2}");
        if (stats.AvgOutdoorTemp.HasValue) sb.AppendLine($"Outdoor temp: avg {stats.AvgOutdoorTemp:F1}°C, min {stats.MinOutdoorTemp:F1}°C, max {stats.MaxOutdoorTemp:F1}°C");
        if (stats.TotalEnergyInKwh.HasValue) sb.AppendLine($"Electricity consumed: {stats.TotalEnergyInKwh:F1} kWh");
        if (stats.TotalHeatOutKwh.HasValue) sb.AppendLine($"Heat delivered: {stats.TotalHeatOutKwh:F1} kWh");
        if (stats.AvgFlowTemp.HasValue) sb.AppendLine($"Avg flow temp: {stats.AvgFlowTemp:F1}°C");
        if (stats.AvgRoomTemp.HasValue && stats.AvgSetpoint.HasValue) sb.AppendLine($"Room temp: avg {stats.AvgRoomTemp:F1}°C vs setpoint {stats.AvgSetpoint:F1}°C");
        if (stats.DutyCyclePercent.HasValue) sb.AppendLine($"Heating duty cycle: {stats.DutyCyclePercent:F1}%");
        if (stats.WeatherCompEnabled.HasValue)
        {
            sb.Append($"Weather compensation: {(stats.WeatherCompEnabled.Value ? "enabled" : "disabled")}");
            if (stats.WcMin.HasValue && stats.WcMax.HasValue) sb.Append($" (range {stats.WcMin:F0}–{stats.WcMax:F0}°C)");
            sb.AppendLine();
        }
    }

    private static AiSummaryDto ParseResponse(string response)
    {
        var dto = new AiSummaryDto();

        var sections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        var currentContent = new StringBuilder();

        foreach (var line in response.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[WEEK]", StringComparison.OrdinalIgnoreCase)) { SaveSection(); currentSection = "WEEK"; continue; }
            if (trimmed.StartsWith("[MONTH]", StringComparison.OrdinalIgnoreCase)) { SaveSection(); currentSection = "MONTH"; continue; }
            if (trimmed.StartsWith("[YEAR]", StringComparison.OrdinalIgnoreCase)) { SaveSection(); currentSection = "YEAR"; continue; }
            if (trimmed.StartsWith("[SUGGESTIONS]", StringComparison.OrdinalIgnoreCase)) { SaveSection(); currentSection = "SUGGESTIONS"; continue; }
            if (currentSection != null) currentContent.AppendLine(line);
        }
        SaveSection();

        dto.WeekSummary = sections.GetValueOrDefault("WEEK", "").Trim();
        dto.MonthSummary = sections.GetValueOrDefault("MONTH", "").Trim();
        dto.YearSummary = sections.GetValueOrDefault("YEAR", "").Trim();
        dto.Suggestions = sections.GetValueOrDefault("SUGGESTIONS", "").Trim();

        // Fallback: if parsing failed, put the whole response in WeekSummary
        if (string.IsNullOrEmpty(dto.WeekSummary) && string.IsNullOrEmpty(dto.MonthSummary))
            dto.WeekSummary = response.Trim();

        return dto;

        void SaveSection()
        {
            if (currentSection != null)
                sections[currentSection] = currentContent.ToString();
            currentContent.Clear();
        }
    }

    private sealed class PeriodStats
    {
        public int SnapshotCount { get; set; }
        public double? AvgCop { get; set; }
        public double? MinCop { get; set; }
        public double? MaxCop { get; set; }
        public double? AvgOutdoorTemp { get; set; }
        public double? MinOutdoorTemp { get; set; }
        public double? MaxOutdoorTemp { get; set; }
        public double? TotalEnergyInKwh { get; set; }
        public double? TotalHeatOutKwh { get; set; }
        public double? AvgFlowTemp { get; set; }
        public double? AvgRoomTemp { get; set; }
        public double? AvgSetpoint { get; set; }
        public double? DutyCyclePercent { get; set; }
        public bool? WeatherCompEnabled { get; set; }
        public double? WcMin { get; set; }
        public double? WcMax { get; set; }
    }
}
