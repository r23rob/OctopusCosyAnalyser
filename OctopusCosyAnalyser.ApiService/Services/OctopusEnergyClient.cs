using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Services;

public class OctopusEnergyClient
{
    private sealed record TokenCacheEntry(string Token, DateTime ExpiresAt);

    private static readonly ConcurrentDictionary<string, TokenCacheEntry> TokenCache = new();

    private readonly HttpClient _httpClient;

    public OctopusEnergyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("https://api.octopus.energy/v1/graphql/");
    }

    // ── Authentication ───────────────────────────────────────────────

    private async Task<string> GetAuthTokenAsync(string apiKey)
    {
        if (TokenCache.TryGetValue(apiKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            return cached.Token;

        var mutation = $$"""
        {
            "query": "mutation { obtainKrakenToken(input: {APIKey: \"{{apiKey}}\"}) { token } }"
        }
        """;

        var content = new StringContent(mutation, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(result);
        var token = json.RootElement.GetProperty("data").GetProperty("obtainKrakenToken").GetProperty("token").GetString();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Octopus API token was empty.");

        var entry = new TokenCacheEntry(token, DateTime.UtcNow.AddMinutes(55));
        TokenCache[apiKey] = entry;

        return token;
    }

    // ── Account & Device Discovery ───────────────────────────────────

    /// <summary>
    /// Gets electricity agreements, meter points, MPAN, serial numbers, and smart device IDs.
    /// Used during device setup to discover the smart meter and device ID.
    /// </summary>
    public async Task<JsonDocument> GetAccountAsync(string apiKey, string accountNumber)
    {
        var query = $$"""
        {
            "query": "query { account(accountNumber: \"{{accountNumber}}\") { electricityAgreements(active: true) { meterPoint { mpan meters(includeInactive: false) { serialNumber smartDevices { deviceId } } } } } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    /// <summary>
    /// Gets account properties and occupierEuids (needed to find the heat pump EUID).
    /// </summary>
    public async Task<JsonDocument> GetViewerPropertiesAsync(string apiKey)
    {
        var query = """
        {
            "query": "query { viewer { accounts { number properties { id occupierEuids } } } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    /// <summary>
    /// Gets EUIDs directly from the heat pump controller API. Fallback when viewer query doesn't return EUIDs.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpControllerEuidsAsync(string apiKey, string accountNumber)
    {
        var query = $$"""
        {
            "query": "query { octoHeatPumpControllerEuids(accountNumber: \"{{accountNumber}}\") }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    /// <summary>
    /// Gets heat pump device info (serial, make, model) by property ID.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpDeviceAsync(string apiKey, string accountNumber, int propertyId)
    {
        var query = $$"""
        {
            "query": "query { heatPumpDevice(accountNumber: \"{{accountNumber}}\", propertyId: {{propertyId}}) { id serialNumber make model installationDate } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    /// <summary>
    /// Gets viewer properties including heat pump device details and EUIDs.
    /// NOTE: This queries the viewer/properties, not the octoHeatPumpControllerConfiguration API.
    /// </summary>
    public async Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(string apiKey)
    {
        var query = """
        {
            "query": "query { viewer { accounts { number properties { id heatPumpDevice { id serialNumber make model } occupierEuids } } } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    // ── Smart Meter ──────────────────────────────────────────────────

    /// <summary>
    /// Gets live smart meter telemetry: consumption, consumptionDelta, demand.
    /// </summary>
    public async Task<JsonDocument> GetSmartMeterTelemetryAsync(string apiKey, string deviceId)
    {
        var query = $$"""
        {
            "query": "query { smartMeterTelemetry(deviceId: \"{{deviceId}}\") { readAt consumption consumptionDelta demand } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    /// <summary>
    /// Gets historical half-hourly consumption data via REST API (Basic auth, not GraphQL).
    /// </summary>
    public async Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to)
    {
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var url = $"https://api.octopus.energy/v1/electricity-meter-points/{mpan}/meters/{serialNumber}/consumption/?period_from={fromStr}&period_to={toStr}&page_size=25000";

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:")));

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(result);
    }

    // ── Heat Pump – Live Status (Basic) ──────────────────────────────

    /// <summary>
    /// Gets basic heat pump status: isConnected, climateControlStatus, waterTemperatureStatus.
    /// Uses the older heatPumpStatus query (doesn't require EUID).
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpStatusAsync(string apiKey)
    {
        var query = """
        {
            "query": "query { heatPumpStatus { isConnected climateControlStatus { climateControlEnabled targetClimateControlTemperature currentClimateControlTemperature } waterTemperatureStatus { climateControlEnabled targetClimateControlTemperature currentClimateControlTemperature } } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    /// <summary>
    /// Lists available heat pump makes and models.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpVariantsAsync(string apiKey, string? make = null)
    {
        var makeFilter = string.IsNullOrEmpty(make) ? "" : $"(make: \\\"{make}\\\")";
        var query = $$"""
        {
            "query": "query { heatPumpVariants{{makeFilter}} { make models { model } } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    // ── Heat Pump – Full Live Data (Primary Workhorse) ───────────────

    /// <summary>
    /// PRIMARY QUERY — batches 4 GraphQL queries in one call:
    ///   1. octoHeatPumpControllerStatus  — sensors (temp, humidity, connectivity), zone telemetry
    ///   2. octoHeatPumpControllerConfiguration — controller state, heat pump details, flow temps, weather comp, zones
    ///   3. octoHeatPumpLivePerformance — live COP, power input, heat output, outdoor temp
    ///   4. octoHeatPumpLifetimePerformance — seasonal COP, lifetime energy totals
    /// Used by /summary endpoint and the HeatPumpSnapshotWorker.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(string apiKey, string accountNumber, string euid)
    {
        var query = $$"""
        {
            "query": "query { octoHeatPumpControllerStatus(accountNumber: \"{{accountNumber}}\", euid: \"{{euid}}\") { sensors { code connectivity { online retrievedAt } telemetry { temperatureInCelsius humidityPercentage retrievedAt } } zones { zone telemetry { setpointInCelsius mode relaySwitchedOn heatDemand retrievedAt } } } octoHeatPumpControllerConfiguration(accountNumber: \"{{accountNumber}}\", euid: \"{{euid}}\") { controller { state heatPumpTimezone connected } heatPump { serialNumber model hardwareVersion maxWaterSetpoint minWaterSetpoint heatingFlowTemperature { currentTemperature { value unit } allowableRange { minimum { value unit } maximum { value unit } } } weatherCompensation { enabled currentRange { minimum { value unit } maximum { value unit } } } } zones { configuration { code zoneType enabled displayName primarySensor currentOperation { mode setpointInCelsius action end } callForHeat heatDemand emergency sensors { ... on ADCSensorConfiguration { code displayName type enabled } ... on ZigbeeSensorConfiguration { code displayName type firmwareVersion boostEnabled } } } } } octoHeatPumpLivePerformance(euid: \"{{euid}}\") { coefficientOfPerformance outdoorTemperature { value unit } heatOutput { value unit } powerInput { value unit } readAt } octoHeatPumpLifetimePerformance(euid: \"{{euid}}\") { seasonalCoefficientOfPerformance heatOutput { value unit } energyInput { value unit } readAt } }"
        }
        """;

        return await ExecuteQueryAsync(apiKey, query);
    }

    // ── Heat Pump – Historic Performance ─────────────────────────────

    /// <summary>
    /// Gets aggregated performance for a date range (single totals, no time buckets).
    /// Returns: coefficientOfPerformance, energyOutput, energyInput.
    /// NOTE: Does NOT have a performanceGrouping parameter.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpTimeRangedPerformanceAsync(string apiKey, string euid, DateTime from, DateTime to)
    {
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var query = """
        query OctoHeatPumpTimeRangedPerformance(
          $euid: ID!,
          $startAt: DateTime!,
          $endAt: DateTime!
        ) {
          octoHeatPumpTimeRangedPerformance(
            euid: $euid,
            startAt: $startAt,
            endAt: $endAt
          ) {
            coefficientOfPerformance
            energyOutput { value unit }
            energyInput { value unit }
          }
        }
        """;

        var variables = new
        {
            euid,
            startAt = fromStr,
            endAt = toStr
        };

        return await ExecuteRawQueryAsync(apiKey, query, JsonSerializer.SerializeToElement(variables));
    }

    /// <summary>
    /// Gets time-bucketed performance data for charting.
    /// Returns: startAt, endAt, energyInput, energyOutput, outdoorTemperature per bucket.
    /// COP is NOT returned per bucket — compute client-side as energyOutput / energyInput.
    ///
    /// PerformanceGrouping controls the BUCKET SIZE (not the date range):
    ///   LIVE  → sub-hourly buckets (best for &lt; 1 day ranges)
    ///   DAY   → 1-hour buckets    (e.g. 24 rows for a single day)
    ///   WEEK  → 1-day buckets     (e.g. 7 rows for a week)
    ///   MONTH → 1-day buckets     (e.g. 30 rows for a month)
    ///   YEAR  → 1-month buckets   (e.g. 12 rows for a year)
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpTimeSeriesPerformanceAsync(string apiKey, string euid, DateTime from, DateTime to, string? performanceGroupingOverride = null)
    {
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        string performanceGrouping;
        if (!string.IsNullOrEmpty(performanceGroupingOverride))
        {
            performanceGrouping = performanceGroupingOverride;
        }
        else
        {
            // Auto-select grouping to keep a sensible number of data points for the range
            var duration = to - from;
            performanceGrouping = duration.TotalDays switch
            {
                < 1 => "LIVE",           // Sub-hourly buckets
                <= 2 => "DAY",           // 1-hour buckets (~24–48 rows)
                <= 60 => "WEEK",         // 1-day buckets (~7–60 rows)
                <= 365 => "MONTH",       // 1-day buckets (~30–365 rows)
                _ => "YEAR"              // 1-month buckets
            };
        }

        var query = """
        query OctoHeatPumpTimeSeriesPerformance(
          $euid: ID!,
          $startAt: DateTime!,
          $endAt: DateTime!,
          $performanceGrouping: PerformanceGrouping!
        ) {
          octoHeatPumpTimeSeriesPerformance(
            euid: $euid,
            startAt: $startAt,
            endAt: $endAt,
            performanceGrouping: $performanceGrouping
          ) {
            startAt
            endAt
            energyInput { value unit }
            energyOutput { value unit }
            outdoorTemperature { value unit }
          }
        }
        """;

        var variables = new
        {
            euid,
            startAt = fromStr,
            endAt = toStr,
            performanceGrouping
        };

        return await ExecuteRawQueryAsync(apiKey, query, JsonSerializer.SerializeToElement(variables));
    }

    // ── Raw / Generic ────────────────────────────────────────────────

    /// <summary>
    /// Executes an arbitrary GraphQL query with optional variables.
    /// Used by the /graphql pass-through endpoint and by parameterised queries above.
    /// </summary>
    public async Task<JsonDocument> ExecuteRawQueryAsync(string apiKey, string query, JsonElement? variables = null)
    {
        var payload = variables.HasValue
            ? JsonSerializer.Serialize(new { query, variables = variables.Value })
            : JsonSerializer.Serialize(new { query });

        return await ExecuteQueryAsync(apiKey, payload);
    }

    // ── Transport ────────────────────────────────────────────────────

    private async Task<JsonDocument> ExecuteQueryAsync(string apiKey, string query)
    {
        var token = await GetAuthTokenAsync(apiKey);

        var content = new StringContent(query, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("JWT", token);

        var response = await _httpClient.PostAsync("", content);
        var result = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Octopus API returned {(int)response.StatusCode}: {result}");

        return JsonDocument.Parse(result);
    }
}
