using System.Text;
using System.Text.Json;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public class AiAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiAnalysisService> _logger;
    private readonly bool _isConfigured;

    private const string SystemPrompt = """
        You are a heat pump performance analyst specialising in the Octopus Energy Cosy heat pump system (Mitsubishi Ecodan under the hood). You analyse daily aggregated data from a UK residential air-source heat pump installation.

        ## Key Concepts

        **COP (Coefficient of Performance)**: heat output / electrical input. Higher is better.
        - COP 3.0 is the threshold where a heat pump matches a gas boiler on running cost at typical UK energy prices (~7p/kWh gas vs ~24p/kWh electricity).
        - Expected COP by outdoor temperature for a well-tuned ASHP:
          - 10°C+ outdoor: COP 3.5–4.5
          - 5–10°C outdoor: COP 3.0–3.5
          - 0–5°C outdoor: COP 2.5–3.0
          - Below 0°C: COP 2.0–2.5
        - If actual COP is significantly below these benchmarks for the given outdoor temp, the system is underperforming.

        **Weather Compensation (WC)**: The system automatically adjusts flow temperature based on outdoor temperature. Lower flow temps = higher COP.
        - WC Min: the minimum flow temperature (used in mild weather). Typical range: 25–35°C.
        - WC Max: the maximum flow temperature (used in cold weather). Typical range: 40–55°C.
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

    public AiAnalysisService(HttpClient httpClient, ILogger<AiAnalysisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _isConfigured = _httpClient.BaseAddress is not null;
    }

    public async Task<string> AnalyseAsync(List<DailyAggregateDto> aggregates, string? userQuestion, CancellationToken ct = default)
    {
        if (!_isConfigured)
        {
            return "AI analysis is not configured. Set the `ANTHROPIC_API_KEY` environment variable and restart the application.";
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
            var response = await _httpClient.PostAsJsonAsync("v1/messages", requestBody, ct);

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

    private static string BuildCsv(List<DailyAggregateDto> aggregates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Snapshots,AvgCOP_Heating,AvgCOP_HotWater,AvgCOP_SpaceHeatingOnly,ElecKwh,HeatKwh,AvgOutdoorC,MinOutdoorC,MaxOutdoorC,AvgFlowC,AvgRoomC,AvgSetpointC,HeatingDuty%,HotWaterDuty%,WC_MinC,WC_MaxC,StateTransitions,CostPence,UsageKwh,AvgUnitRateP,CostPerKwhHeatP,HW_RunCount,HW_TotalMins,AvgHW_SetpointC");

        foreach (var a in aggregates)
        {
            sb.AppendLine(string.Join(",",
                a.Date.ToString("yyyy-MM-dd"),
                a.SnapshotCount,
                Fmt(a.AvgCopHeating),
                Fmt(a.AvgCopHotWater),
                Fmt(a.AvgCopSpaceHeatingOnly),
                a.TotalElectricityKwh.ToString("F2"),
                a.TotalHeatOutputKwh.ToString("F2"),
                Fmt(a.AvgOutdoorTemp),
                Fmt(a.MinOutdoorTemp),
                Fmt(a.MaxOutdoorTemp),
                Fmt(a.AvgFlowTemp),
                Fmt(a.AvgRoomTemp),
                Fmt(a.AvgSetpoint),
                a.HeatingDutyCyclePercent.ToString("F1"),
                a.HotWaterDutyCyclePercent.ToString("F1"),
                Fmt(a.WeatherCompMin),
                Fmt(a.WeatherCompMax),
                a.ControllerStateTransitions,
                Fmt(a.DailyCostPence),
                Fmt(a.DailyUsageKwh),
                Fmt(a.AvgUnitRatePence),
                Fmt(a.CostPerKwhHeatPence),
                a.HotWaterRunCount,
                a.HotWaterTotalMinutes,
                Fmt(a.AvgHotWaterSetpoint)
            ));
        }

        return sb.ToString();
    }

    private static string Fmt(double? value) => value.HasValue ? value.Value.ToString("F2") : "";
}
