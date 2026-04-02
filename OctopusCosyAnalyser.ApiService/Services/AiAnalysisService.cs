using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public class AiAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiAnalysisService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly bool _isConfigured;

    // Dashboard summary cache (30-minute TTL)
    private readonly ConcurrentDictionary<string, (AiSummaryDto Summary, DateTime CachedAt)> _summaryCache = new();
    private static readonly TimeSpan SummaryCacheDuration = TimeSpan.FromMinutes(30);

    private const string SystemPrompt = """
        You are a heat pump performance analyst specialising in the Octopus Energy Cosy heat pump system (Mitsubishi Ecodan under the hood). You analyse daily aggregated data from a UK residential air-source heat pump installation.

        ## Data Sources

        The CSV data is built from two sources:
        1. **Snapshots** — periodic readings (every ~15 min) capturing live COP, power input, heat output, room temp, setpoint, zone demand, weather compensation settings, flow temperature, and controller state.
        2. **Synced hourly history** — energy input/output and outdoor temperature from the Octopus `octoHeatPumpTimeSeriesPerformance` API, covering up to 12 months. Weather compensation values (WC_MinC, WC_MaxC) on history-only days are correlated from the nearest snapshot within a 30-minute window.

        Days with `SnapshotCount=0` have data from history records only — they will have energy totals and outdoor temperature but may lack duty cycle, room temperature, and zone demand data. Treat these days as having less granular data but still useful for energy trends and WC analysis.

        ## Key Concepts

        **COP (Coefficient of Performance)**: heat output / electrical input. Higher is better.
        - COP 3.0 is the threshold where a heat pump matches a gas boiler on running cost at typical UK energy prices (~7p/kWh gas vs ~24p/kWh electricity).
        - Expected COP by outdoor temperature for a well-tuned ASHP:
          - 10°C+ outdoor: COP 3.5–4.5
          - 5–10°C outdoor: COP 3.0–3.5
          - 0–5°C outdoor: COP 2.5–3.0
          - Below 0°C: COP 2.0–2.5
        - If actual COP is significantly below these benchmarks for the given outdoor temp, the system is underperforming.

        **Weather Compensation (WC) vs Fixed Flow Temperature**:
        The Cosy system has TWO heating modes — only one is active at a time:
        - **WC_Enabled=true**: Weather Compensation is active. The system automatically varies flow temperature between WC_MinC (mild weather) and WC_MaxC (cold weather) based on outdoor temperature. Lower flow temps = higher COP.
        - **WC_Enabled=false**: Fixed flow temperature mode. The system uses a constant flow temperature set to FixedFlowSetpointC regardless of outdoor conditions.
        
        **IMPORTANT**: FixedFlowSetpointC is the CONFIGURED setpoint for fixed mode — it is NOT a measured actual flow temperature. The Octopus API does not provide a measured flow temperature reading. When WC is enabled, the system ignores FixedFlowSetpointC and uses the WC curve instead.
        
        WC curve tuning guidelines:
        - WC Min: the flow temperature used in mild weather. Typical range: 25–35°C.
        - WC Max: the flow temperature used in cold weather. Typical range: 40–55°C.
        - If COP is poor on mild days (>5°C), WC max is likely too high — the system is sending water hotter than needed.
        - If the house can't maintain setpoint on cold days, WC max may need to increase — but try reducing setpoint or improving insulation first.

        **Hot Water (DHW)**:
        - DHW always runs at higher flow temps (typically 45–55°C) and will have lower COP (typically 2.0–2.5).
        - Mixing DHW and space heating COP data obscures true heating efficiency — always analyse them separately.
        - Legionella pasteurisation cycle (60°C) should run at least weekly.
        - DHW scheduling: ideally runs during off-peak tariff periods to minimise cost.

        **Duty Cycle**:
        - 40–80% is normal for space heating in winter.
        - >90% means the system can barely keep up — may need higher WC max or building improvements.
        - <20% in cold weather suggests oversized system or setpoint could be lowered.

        **Controller State Transitions (Cycling)**:
        - >10 transitions/day warrants investigation — suggests short-cycling.
        - Short cycling wastes energy during start-up and reduces compressor lifespan.
        - Causes: oversized system, incorrect WC settings, inadequate buffer tank.

        **Cost Analysis**:
        - Cost per kWh of heat = daily electricity cost / daily heat output kWh.
        - Compare to gas: ~7p/kWh for gas heating. If cost per kWh heat exceeds this, the heat pump is costing more than gas.
        - Octopus Cosy tariff has off-peak windows — shifting DHW and pre-heating to off-peak significantly reduces cost.
        - Average unit rate variation between days may indicate time-of-use tariff benefits not being captured.

        ## Analysis Instructions

        Analyse the provided CSV data and produce a structured report with these sections:

        1. **Summary** — Overall performance assessment in 2-3 sentences.
        2. **COP Performance** — How COP varies with outdoor temperature. Flag any days where COP is significantly below expected benchmarks. Identify the outdoor temperature at which COP drops below 3.0. Compare space-heating-only COP vs overall COP.
        3. **Weather Compensation** — Is the WC curve well-tuned? Look at flow temp vs outdoor temp vs COP. Suggest specific WC min/max adjustments if warranted.
        4. **Hot Water Impact** — How much does DHW drag down overall COP? How many DHW runs per day? Total DHW minutes? Could scheduling be improved?
        5. **Cost Analysis** — Daily running costs, cost per kWh of heat vs gas equivalent. Are there days with notably higher unit rates? Could off-peak usage be improved?
        6. **Comfort** — Is the room maintaining setpoint? Any days where avg room temp is significantly below setpoint?
        7. **Recommendations** — Specific, actionable changes with expected impact. Use concrete numbers (e.g., "reduce WC max from 50°C to 45°C", not "consider adjusting").

        Use markdown formatting with headers, bullet points, and bold for key figures. Be direct and specific — the user is technically competent.
        """;

    public AiAnalysisService(HttpClient httpClient, ILogger<AiAnalysisService> logger, IServiceScopeFactory scopeFactory)
    {
        _httpClient = httpClient;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _isConfigured = _httpClient.BaseAddress is not null;
    }

    public async Task<string> AnalyseAsync(List<DailyAggregateDto> aggregates, string? userQuestion, string? anthropicApiKey = null, CancellationToken ct = default)
    {
        // Determine which API key to use: DB-stored key takes priority, then startup-configured
        var usePerRequestKey = !string.IsNullOrWhiteSpace(anthropicApiKey);

        if (!usePerRequestKey && !_isConfigured)
        {
            return "AI analysis is not configured. Add your Anthropic API key in Account Settings, or set the `ANTHROPIC_API_KEY` environment variable and restart.";
        }

        var csv = BuildCsv(aggregates);

        var userMessage = new StringBuilder();
        userMessage.AppendLine("Here is the daily aggregated heat pump performance data:");
        userMessage.AppendLine();
        userMessage.AppendLine("```csv");
        userMessage.AppendLine(csv);
        userMessage.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(userQuestion))
        {
            userMessage.AppendLine();
            userMessage.AppendLine("Additionally, please address this specific question:");
            userMessage.AppendLine(userQuestion);
        }

        var requestBody = new
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 4096,
            system = SystemPrompt,
            messages = new[]
            {
                new { role = "user", content = userMessage.ToString() }
            }
        };

        try
        {
            HttpResponseMessage response;

            if (usePerRequestKey)
            {
                // Use per-request API key from DB — send directly to Anthropic
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                requestMessage.Headers.Add("x-api-key", anthropicApiKey);
                requestMessage.Headers.Add("anthropic-version", "2023-06-01");
                requestMessage.Content = JsonContent.Create(requestBody);
                response = await _httpClient.SendAsync(requestMessage, ct);
            }
            else
            {
                // Use startup-configured client (key already in default headers)
                response = await _httpClient.PostAsJsonAsync("v1/messages", requestBody, ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Anthropic API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                return $"AI analysis failed (HTTP {(int)response.StatusCode}). Check the API key and try again.";
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            if (responseJson is null)
                return "AI analysis returned an empty response.";

            var content = responseJson.RootElement.GetProperty("content");
            if (content.GetArrayLength() > 0)
            {
                var textBlock = content[0];
                if (textBlock.TryGetProperty("text", out var text))
                    return text.GetString() ?? "No analysis text returned.";
            }

            return "AI analysis returned an unexpected response format.";
        }
        catch (TaskCanceledException)
        {
            return "AI analysis timed out. Try a shorter date range.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI analysis failed");
            return $"AI analysis failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates a cached dashboard summary using daily aggregates for 7/30/365 day periods.
    /// Reuses the same rich system prompt and daily aggregate pipeline as the detailed analysis.
    /// </summary>
    public async Task<AiSummaryDto> GenerateDashboardSummaryAsync(string deviceId, bool forceRefresh = false, string? anthropicApiKey = null)
    {
        if (!forceRefresh && _summaryCache.TryGetValue(deviceId, out var cached) && DateTime.UtcNow - cached.CachedAt < SummaryCacheDuration)
            return cached.Summary;

        var usePerRequestKey = !string.IsNullOrWhiteSpace(anthropicApiKey);
        if (!usePerRequestKey && !_isConfigured)
        {
            return new AiSummaryDto
            {
                WeekSummary = "AI summaries are not available. Add your Anthropic API key in Account Settings, or set the ANTHROPIC_API_KEY environment variable.",
                GeneratedAt = DateTime.UtcNow
            };
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();

            var now = DateTime.UtcNow;
            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= now.AddDays(-365))
                .OrderBy(s => s.SnapshotTakenAt)
                .ToListAsync();

            if (snapshots.Count == 0)
            {
                return new AiSummaryDto
                {
                    WeekSummary = "No snapshot data available yet. The background worker captures data every 15 minutes.",
                    GeneratedAt = DateTime.UtcNow
                };
            }

            // Build daily aggregates for each period using the same pipeline as detailed analysis
            var weekSnapshots = snapshots.Where(s => s.SnapshotTakenAt >= now.AddDays(-7)).ToList();
            var monthSnapshots = snapshots.Where(s => s.SnapshotTakenAt >= now.AddDays(-30)).ToList();

            var weekAggs = HeatPumpEndpoints.ComputeDailyAggregates(weekSnapshots);
            var monthAggs = HeatPumpEndpoints.ComputeDailyAggregates(monthSnapshots);
            var yearAggs = HeatPumpEndpoints.ComputeDailyAggregates(snapshots);

            var prompt = new StringBuilder();
            prompt.AppendLine("Provide a BRIEF dashboard summary of this heat pump's performance across three time periods.");
            prompt.AppendLine("Each section should be 2-3 sentences max. Be specific with numbers. Use plain English.");
            prompt.AppendLine();
            prompt.AppendLine("=== LAST 7 DAYS ===");
            prompt.AppendLine("```csv");
            prompt.AppendLine(BuildCsv(weekAggs));
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("=== LAST 30 DAYS ===");
            prompt.AppendLine("```csv");
            prompt.AppendLine(BuildCsv(monthAggs));
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("=== LAST 365 DAYS ===");
            prompt.AppendLine("```csv");
            prompt.AppendLine(BuildCsv(yearAggs));
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("Respond in EXACTLY this format with 4 sections:");
            prompt.AppendLine();
            prompt.AppendLine("[WEEK]");
            prompt.AppendLine("Summary of last 7 days.");
            prompt.AppendLine();
            prompt.AppendLine("[MONTH]");
            prompt.AppendLine("Summary of last 30 days.");
            prompt.AppendLine();
            prompt.AppendLine("[YEAR]");
            prompt.AppendLine("Summary of last 365 days.");
            prompt.AppendLine();
            prompt.AppendLine("[SUGGESTIONS]");
            prompt.AppendLine("Specific, actionable suggestions. If performance is good, say so.");

            var requestBody = new
            {
                model = "claude-sonnet-4-20250514",
                max_tokens = 1500,
                system = SystemPrompt,
                messages = new[] { new { role = "user", content = prompt.ToString() } }
            };

            HttpResponseMessage response;
            if (usePerRequestKey)
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                requestMessage.Headers.Add("x-api-key", anthropicApiKey);
                requestMessage.Headers.Add("anthropic-version", "2023-06-01");
                requestMessage.Content = JsonContent.Create(requestBody);
                response = await _httpClient.SendAsync(requestMessage);
            }
            else
            {
                response = await _httpClient.PostAsJsonAsync("v1/messages", requestBody);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Dashboard summary API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                return new AiSummaryDto
                {
                    WeekSummary = $"AI summary failed (HTTP {(int)response.StatusCode}). Check the API key.",
                    GeneratedAt = DateTime.UtcNow
                };
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonDocument>();
            var responseText = "";
            if (responseJson is not null)
            {
                var content = responseJson.RootElement.GetProperty("content");
                if (content.GetArrayLength() > 0 && content[0].TryGetProperty("text", out var text))
                    responseText = text.GetString() ?? "";
            }

            var summary = ParseSummaryResponse(responseText);
            summary.GeneratedAt = DateTime.UtcNow;
            _summaryCache[deviceId] = (summary, DateTime.UtcNow);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate dashboard summary for device {DeviceId}", deviceId);
            return new AiSummaryDto
            {
                WeekSummary = $"Failed to generate AI summary: {ex.Message}",
                GeneratedAt = DateTime.UtcNow
            };
        }
    }

    private static AiSummaryDto ParseSummaryResponse(string response)
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

    private static string BuildCsv(List<DailyAggregateDto> aggregates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Snapshots,AvgCOP_Heating,AvgCOP_HotWater,AvgCOP_SpaceHeatingOnly,ElecKwh,HeatKwh,AvgOutdoorC,MinOutdoorC,MaxOutdoorC,FixedFlowSetpointC,AvgRoomC,AvgSetpointC,HeatingDuty%,HotWaterDuty%,WC_Enabled,WC_MinC,WC_MaxC,StateTransitions,CostPence,UsageKwh,AvgUnitRateP,CostPerKwhHeatP,HW_RunCount,HW_TotalMins,AvgHW_SetpointC");

        foreach (var a in aggregates)
        {
            sb.AppendLine(string.Join(",",
                a.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                a.SnapshotCount.ToString(CultureInfo.InvariantCulture),
                Fmt(a.AvgCopHeating),
                Fmt(a.AvgCopHotWater),
                Fmt(a.AvgCopSpaceHeatingOnly),
                a.TotalElectricityKwh.ToString("F2", CultureInfo.InvariantCulture),
                a.TotalHeatOutputKwh.ToString("F2", CultureInfo.InvariantCulture),
                Fmt(a.AvgOutdoorTemp),
                Fmt(a.MinOutdoorTemp),
                Fmt(a.MaxOutdoorTemp),
                Fmt(a.AvgFlowTemp),
                Fmt(a.AvgRoomTemp),
                Fmt(a.AvgSetpoint),
                a.HeatingDutyCyclePercent.ToString("F1", CultureInfo.InvariantCulture),
                a.HotWaterDutyCyclePercent.ToString("F1", CultureInfo.InvariantCulture),
                a.WeatherCompEnabled.HasValue ? (a.WeatherCompEnabled.Value ? "true" : "false") : "",
                Fmt(a.WeatherCompMin),
                Fmt(a.WeatherCompMax),
                a.ControllerStateTransitions.ToString(CultureInfo.InvariantCulture),
                Fmt(a.DailyCostPence),
                Fmt(a.DailyUsageKwh),
                Fmt(a.AvgUnitRatePence),
                Fmt(a.CostPerKwhHeatPence),
                a.HotWaterRunCount.ToString(CultureInfo.InvariantCulture),
                a.HotWaterTotalMinutes.ToString(CultureInfo.InvariantCulture),
                Fmt(a.AvgHotWaterSetpoint)
            ));
        }

        return sb.ToString();
    }

    private static string Fmt(double? value) => value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "";
}
