using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public class OctopusEnergyClient : IOctopusEnergyClient
{
    private sealed record TokenCacheEntry(string Token, DateTime ExpiresAt);

    private static readonly ConcurrentDictionary<string, TokenCacheEntry> TokenCache = new();

    // Serialize token acquisition so only one request is in-flight at a time
    // (prevents concurrent workers from racing and obtaining multiple tokens,
    //  which can cause the Octopus API to invalidate earlier tokens)
    private static readonly SemaphoreSlim TokenSemaphore = new(1, 1);

    // Allowlist: alphanumeric, hyphens, underscores, dots — defence-in-depth input validation
    // (no longer the primary injection guard; all queries now use parameterised GraphQL variables)
    private static readonly Regex SafeIdentifierRegex = new(@"^[A-Za-z0-9\-_.]{1,200}$", RegexOptions.Compiled);

    private static void ValidateIdentifier(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value) || !SafeIdentifierRegex.IsMatch(value))
            throw new ArgumentException($"Invalid value for '{paramName}': must be 1–200 alphanumeric, hyphen, underscore, or dot characters.", paramName);
    }

    private readonly HttpClient _httpClient;
    private readonly ILogger<OctopusEnergyClient> _logger;

    public OctopusEnergyClient(HttpClient httpClient, ILogger<OctopusEnergyClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://api.backend.octopus.energy/v1/graphql/");
    }

    // ── Authentication ───────────────────────────────────────────────

    /// <summary>
    /// Returns a cache key for the given settings — uses API key or email depending on auth mode.
    /// </summary>
    private static string GetCacheKey(OctopusAccountSettings settings) =>
        settings.AuthMode == "apikey" ? $"apikey:{settings.ApiKey}" : $"email:{settings.Email}";

    private async Task<string> GetAuthTokenAsync(OctopusAccountSettings settings)
    {
        if (settings.AuthMode == "apikey")
        {
            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new ArgumentException("API key is required for API key authentication.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.Email))
                throw new ArgumentException("Email is required for password authentication.");
            if (string.IsNullOrWhiteSpace(settings.OctopusPassword))
                throw new ArgumentException("Password is required for password authentication.");
        }

        var cacheKey = GetCacheKey(settings);

        // Fast path: return cached token without acquiring lock
        if (TokenCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresAt)
            return cached.Token;

        // Serialize token acquisition — prevents multiple workers from racing to
        // obtain separate tokens (which can cause the API to invalidate earlier ones)
        await TokenSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock — another thread may have cached a token
            if (TokenCache.TryGetValue(cacheKey, out cached) && DateTime.UtcNow < cached.ExpiresAt)
                return cached.Token;

            string payload;
            if (settings.AuthMode == "apikey")
            {
                _logger.LogInformation("Obtaining new Octopus API token via API key");
                var query = "mutation ObtainToken($input: ObtainJSONWebTokenInput!) { obtainKrakenToken(input: $input) { token } }";
                payload = JsonSerializer.Serialize(new
                {
                    query,
                    variables = new { input = new { APIKey = settings.ApiKey } }
                });
            }
            else
            {
                _logger.LogInformation("Obtaining new Octopus API token for {Email}", settings.Email);
                var query = "mutation ObtainToken($input: ObtainJSONWebTokenInput!) { obtainKrakenToken(input: $input) { token } }";
                payload = JsonSerializer.Serialize(new
                {
                    query,
                    variables = new { input = new { email = settings.Email, password = settings.OctopusPassword } }
                });
            }

            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.octopus.energy/v1/graphql/", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(result);
            var token = json.RootElement.GetProperty("data").GetProperty("obtainKrakenToken").GetProperty("token").GetString();

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Octopus API token was empty.");

            var entry = new TokenCacheEntry(token, DateTime.UtcNow.AddMinutes(55));
            TokenCache[cacheKey] = entry;

            return token;
        }
        finally
        {
            TokenSemaphore.Release();
        }
    }

    // ── Account & Device Discovery ───────────────────────────────────

    /// <summary>
    /// Gets electricity agreements, meter points, MPAN, serial numbers, and smart device IDs.
    /// Used during device setup to discover the smart meter and device ID.
    /// </summary>
    public async Task<JsonDocument> GetAccountAsync(OctopusAccountSettings settings, string accountNumber)
    {
        ValidateIdentifier(accountNumber, nameof(accountNumber));

        var query = """
        query GetAccount($accountNumber: String!) {
          account(accountNumber: $accountNumber) {
            electricityAgreements(active: true) {
              meterPoint {
                mpan
                meters(includeInactive: false) {
                  serialNumber
                  smartDevices { deviceId }
                }
              }
            }
          }
        }
        """;

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(new { accountNumber }));
    }

    /// <summary>
    /// Gets account properties and occupierEuids (needed to find the heat pump EUID).
    /// </summary>
    public async Task<JsonDocument> GetViewerPropertiesAsync(OctopusAccountSettings settings)
    {
        var query = """
        query {
          viewer {
            accounts {
              number
              properties { id occupierEuids }
            }
          }
        }
        """;

        return await ExecuteRawQueryAsync(settings, query);
    }

    /// <summary>
    /// Gets EUIDs directly from the heat pump controller API. Fallback when viewer query doesn't return EUIDs.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpControllerEuidsAsync(OctopusAccountSettings settings, string accountNumber)
    {
        ValidateIdentifier(accountNumber, nameof(accountNumber));

        var query = """
        query GetEuids($accountNumber: String!) {
          heatPumpControllerEuids(accountNumber: $accountNumber)
        }
        """;

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(new { accountNumber }));
    }

    /// <summary>
    /// Gets heat pump device info (serial, make, model) by property ID.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpDeviceAsync(OctopusAccountSettings settings, string accountNumber, int propertyId)
    {
        ValidateIdentifier(accountNumber, nameof(accountNumber));

        var query = """
        query GetHeatPumpDevice($accountNumber: String!, $propertyId: Int!) {
          heatPumpDevice(accountNumber: $accountNumber, propertyId: $propertyId) {
            id serialNumber make model installationDate
          }
        }
        """;

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(new { accountNumber, propertyId }));
    }

    /// <summary>
    /// Gets viewer properties including heat pump device details and EUIDs.
    /// NOTE: This queries the viewer/properties, not the heatPumpControllerConfiguration API.
    /// </summary>
    public async Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(OctopusAccountSettings settings)
    {
        var query = """
        query {
          viewer {
            accounts {
              number
              properties {
                id
                heatPumpDevice { id serialNumber make model }
                occupierEuids
              }
            }
          }
        }
        """;

        return await ExecuteRawQueryAsync(settings, query);
    }

    // ── Smart Meter ──────────────────────────────────────────────────

    /// <summary>
    /// Gets live smart meter telemetry: consumption, consumptionDelta, demand.
    /// </summary>
    public async Task<JsonDocument> GetSmartMeterTelemetryAsync(OctopusAccountSettings settings, string deviceId)
    {
        ValidateIdentifier(deviceId, nameof(deviceId));

        var query = """
        query GetSmartMeterTelemetry($deviceId: String!) {
          smartMeterTelemetry(deviceId: $deviceId) {
            readAt consumption consumptionDelta demand
          }
        }
        """;

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(new { deviceId }));
    }

    /// <summary>
    /// Gets historical half-hourly consumption data via REST API (Basic auth, not GraphQL).
    /// Follows pagination automatically to retrieve all results across all pages.
    /// </summary>
    public async Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to)
    {
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var allResults = new List<JsonElement>();
        string? url = $"https://api.octopus.energy/v1/electricity-meter-points/{mpan}/meters/{serialNumber}/consumption/?period_from={fromStr}&period_to={toStr}&page_size=25000";
        var authHeader = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:")));

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = authHeader;

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            using var page = JsonDocument.Parse(body);

            if (page.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var item in results.EnumerateArray())
                    allResults.Add(item.Clone());
            }

            // Follow the "next" URL for additional pages
            url = page.RootElement.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        // Reconstruct a response matching the original single-page shape
        var combined = JsonSerializer.Serialize(new { count = allResults.Count, next = (string?)null, previous = (string?)null, results = allResults });
        return JsonDocument.Parse(combined);
    }

    // ── Heat Pump – Live Status (Basic) ──────────────────────────────

    /// <summary>
    /// Gets basic heat pump status: isConnected, climateControlStatus, waterTemperatureStatus.
    /// Uses the older heatPumpStatus query (doesn't require EUID).
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpStatusAsync(OctopusAccountSettings settings)
    {
        var query = """
        query {
          heatPumpStatus {
            isConnected
            climateControlStatus {
              climateControlEnabled
              targetClimateControlTemperature
              currentClimateControlTemperature
            }
            waterTemperatureStatus {
              climateControlEnabled
              targetClimateControlTemperature
              currentClimateControlTemperature
            }
          }
        }
        """;

        return await ExecuteRawQueryAsync(settings, query);
    }

    /// <summary>
    /// Lists available heat pump makes and models.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpVariantsAsync(OctopusAccountSettings settings, string? make = null)
    {
        if (make != null)
            ValidateIdentifier(make, nameof(make));

        if (!string.IsNullOrEmpty(make))
        {
            var query = """
            query GetHeatPumpVariants($make: String!) {
              heatPumpVariants(make: $make) {
                make
                models { model }
              }
            }
            """;

            return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(new { make }));
        }
        else
        {
            var query = """
            query {
              heatPumpVariants {
                make
                models { model }
              }
            }
            """;

            return await ExecuteRawQueryAsync(settings, query);
        }
    }

    // ── Heat Pump – Full Live Data (Primary Workhorse) ───────────────

    /// <summary>
    /// PRIMARY QUERY — batches 4 GraphQL queries in one call:
    ///   1. heatPumpControllerStatus      — sensors (temp, humidity, connectivity), zone telemetry
    ///   2. heatPumpControllerConfiguration — controller state, heat pump details, flow temps, weather comp, zones
    ///   3. heatPumpTimeSeriesPerformance (LIVE) — recent energy in/out, outdoor temp (COP computed client-side)
    ///   4. heatPumpLifetimePerformance   — seasonal COP, lifetime energy totals
    /// Used by /summary endpoint and the HeatPumpSnapshotWorker.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(OctopusAccountSettings settings, string accountNumber, string euid)
    {
        var now = DateTime.UtcNow;
        var liveStartAt = now.AddMinutes(-30).ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var liveEndAt = now.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var query = """
        query HeatPumpStatusAndConfig($accountNumber: String!, $euid: ID!, $liveStartAt: DateTime!, $liveEndAt: DateTime!) {
          heatPumpControllerStatus(accountNumber: $accountNumber, euid: $euid) {
            sensors {
              code
              connectivity { online retrievedAt }
              telemetry { temperatureInCelsius humidityPercentage retrievedAt }
            }
            zones {
              zone
              telemetry { setpointInCelsius mode relaySwitchedOn heatDemand retrievedAt }
            }
          }
          heatPumpControllerConfiguration(accountNumber: $accountNumber, euid: $euid) {
            controller { state heatPumpTimezone connected }
            heatPump {
              serialNumber model hardwareVersion maxWaterSetpoint minWaterSetpoint
              heatingFlowTemperature {
                currentTemperature { value unit }
                allowableRange { minimum { value unit } maximum { value unit } }
              }
              weatherCompensation {
                enabled
                currentRange { minimum { value unit } maximum { value unit } }
              }
            }
            zones {
              configuration {
                code zoneType enabled displayName primarySensor
                currentOperation { mode setpointInCelsius action end }
                callForHeat heatDemand emergency
                sensors {
                  ... on ADCSensorConfiguration { code displayName type enabled }
                  ... on ZigbeeSensorConfiguration { code displayName type firmwareVersion boostEnabled }
                }
              }
            }
          }
          heatPumpTimeSeriesPerformance(accountNumber: $accountNumber, euid: $euid, startAt: $liveStartAt, endAt: $liveEndAt, performanceGrouping: LIVE) {
            startAt
            endAt
            energyInput { value unit }
            energyOutput { value unit }
            outdoorTemperature { value unit }
          }
          heatPumpLifetimePerformance(accountNumber: $accountNumber, euid: $euid) {
            seasonalCoefficientOfPerformance
            heatOutput { value unit }
            energyInput { value unit }
            readAt
          }
        }
        """;

        var variables = new { accountNumber, euid, liveStartAt, liveEndAt };

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(variables));
    }

    // ── Heat Pump – Historic Performance ─────────────────────────────

    /// <summary>
    /// Gets aggregated performance for a date range (single totals, no time buckets).
    /// Returns: coefficientOfPerformance, energyOutput, energyInput.
    /// NOTE: Does NOT have a performanceGrouping parameter.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpTimeRangedPerformanceAsync(OctopusAccountSettings settings, string accountNumber, string euid, DateTime from, DateTime to)
    {
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var query = """
        query HeatPumpTimeRangedPerformance(
          $accountNumber: String!,
          $euid: ID!,
          $startAt: DateTime!,
          $endAt: DateTime!
        ) {
          heatPumpTimeRangedPerformance(
            accountNumber: $accountNumber,
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
            accountNumber,
            euid,
            startAt = fromStr,
            endAt = toStr
        };

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(variables));
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
    public async Task<JsonDocument> GetHeatPumpTimeSeriesPerformanceAsync(OctopusAccountSettings settings, string accountNumber, string euid, DateTime from, DateTime to, string? performanceGroupingOverride = null)
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
        query HeatPumpTimeSeriesPerformance(
          $accountNumber: String!,
          $euid: ID!,
          $startAt: DateTime!,
          $endAt: DateTime!,
          $performanceGrouping: PerformanceGrouping!
        ) {
          heatPumpTimeSeriesPerformance(
            accountNumber: $accountNumber,
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
            accountNumber,
            euid,
            startAt = fromStr,
            endAt = toStr,
            performanceGrouping
        };

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(variables));
    }

    // ── Heat Pump – Controllers at Location ──────────────────────────

    /// <summary>
    /// Discovers all heat pump controllers at a given location.
    /// Useful for multi-HP setups and as a fallback during device discovery.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpControllersAtLocationAsync(OctopusAccountSettings settings, string accountNumber, int propertyId)
    {
        var query = """
        query GetControllersAtLocation($accountNumber: String!, $propertyId: Int!) {
          heatPumpControllersAtLocation(accountNumber: $accountNumber, propertyId: $propertyId)
        }
        """;

        return await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(new { accountNumber, propertyId }));
    }

    // ── Tariff Rates ───────────────────────────────────────────────

    /// <summary>
    /// Gets applicable tariff rates for an account and meter point.
    /// Returns rate periods via Relay connection (edges/node pattern).
    /// </summary>
    public async Task<JsonDocument> GetApplicableRatesAsync(OctopusAccountSettings settings, string accountNumber, string mpxn, DateTime startAt, DateTime endAt)
    {
        var startAtStr = startAt.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var endAtStr = endAt.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var query = """
        query ApplicableRates(
          $accountNumber: String!,
          $mpxn: String!,
          $startAt: DateTime!,
          $endAt: DateTime!,
          $first: Int!,
          $after: String
        ) {
          applicableRates(
            accountNumber: $accountNumber,
            mpxn: $mpxn,
            startAt: $startAt,
            endAt: $endAt,
            first: $first,
            after: $after
          ) {
            edges {
              node {
                validFrom
                validTo
                value
              }
            }
            pageInfo {
              hasNextPage
              endCursor
            }
          }
        }
        """;

        // Paginate through all results (API limits first to 100)
        var allEdges = new List<JsonElement>();
        string? cursor = null;

        while (true)
        {
            var variables = new Dictionary<string, object?>
            {
                ["accountNumber"] = accountNumber,
                ["mpxn"] = mpxn,
                ["startAt"] = startAtStr,
                ["endAt"] = endAtStr,
                ["first"] = 100,
                ["after"] = cursor
            };

            var result = await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(variables));
            var root = result.RootElement;

            // If there are errors, return the result as-is
            if (root.TryGetProperty("errors", out _))
                return result;

            var data = root.GetProperty("data");
            var applicableRates = data.GetProperty("applicableRates");

            if (applicableRates.ValueKind == JsonValueKind.Null)
                return result;

            var edges = applicableRates.GetProperty("edges");
            foreach (var edge in edges.EnumerateArray())
                allEdges.Add(edge.Clone());

            var pageInfo = applicableRates.GetProperty("pageInfo");
            var hasNextPage = pageInfo.GetProperty("hasNextPage").GetBoolean();

            if (!hasNextPage)
                break;

            cursor = pageInfo.GetProperty("endCursor").GetString();
            result.Dispose();
        }

        // Build a combined response document preserving original JSON types
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();                          // {
            writer.WritePropertyName("data");
            writer.WriteStartObject();                          //   "data": {
            writer.WritePropertyName("applicableRates");
            writer.WriteStartObject();                          //     "applicableRates": {
            writer.WritePropertyName("edges");
            writer.WriteStartArray();                           //       "edges": [
            foreach (var edge in allEdges)
                edge.WriteTo(writer);                           //         { "node": { ... } }
            writer.WriteEndArray();                             //       ]
            writer.WriteEndObject();                            //     }
            writer.WriteEndObject();                            //   }
            writer.WriteEndObject();                            // }
        }

        return JsonDocument.Parse(ms.ToArray());
    }

    // ── Cost of Usage ──────────────────────────────────────────────

    // Cached schema discovery for costOfUsage query
    private static CostOfUsageSchema? _costOfUsageSchema;

    private sealed record CostOfUsageSchema(
        List<string> ArgNames,
        string? StartArg,
        string? EndArg,
        string ReturnTypeName,
        bool IsConnection,
        bool IsArray,
        List<string> FieldNames,
        List<string> NodeFieldNames);

    /// <summary>
    /// Returns the known costOfUsage schema.
    /// Previously used introspection (__schema / __type queries), but Octopus now blocks
    /// introspection on the backend API (returns 403). The schema is hardcoded instead.
    /// </summary>
    private static CostOfUsageSchema GetCostOfUsageSchema()
    {
        if (_costOfUsageSchema is not null)
            return _costOfUsageSchema;

        var argNames = new List<string>
        {
            "accountNumber", "startAt", "endAt", "grouping",
            "propertyId", "mpxn", "first", "after"
        };

        var nodeFieldNames = new List<string>
        {
            "startAt", "endAt", "costInclTax", "costExclTax",
            "consumptionKwh", "unitRateInclTax", "unitRateExclTax",
            "standingCharge", "costCurrency"
        };

        var connectionFieldNames = new List<string> { "edges", "pageInfo" };

        _costOfUsageSchema = new CostOfUsageSchema(
            argNames,
            StartArg: "startAt",
            EndArg: "endAt",
            ReturnTypeName: "CostOfUsageConnection",
            IsConnection: true,
            IsArray: false,
            FieldNames: connectionFieldNames,
            NodeFieldNames: nodeFieldNames);

        return _costOfUsageSchema;
    }


    /// <summary>
    /// Gets the actual cost of energy usage for a date range.
    /// Uses a hardcoded schema definition (introspection is blocked by the Octopus backend API).
    /// grouping: HALF_HOUR, DAY, WEEK, MONTH, QUARTER
    /// </summary>
    public async Task<JsonDocument> GetCostOfUsageAsync(OctopusAccountSettings settings, string accountNumber, DateTime from, DateTime to, string grouping = "DAY", int? propertyId = null, string? mpxn = null)
    {
        var fromStr = from.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = to.ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var schema = GetCostOfUsageSchema();
        _logger.LogDebug("Cost of usage schema: args=[{Args}], startArg={Start}, endArg={End}, isConnection={IsConn}",
            string.Join(", ", schema.ArgNames), schema.StartArg, schema.EndArg, schema.IsConnection);

        var result = await ExecuteCostOfUsageQueryAsync(settings, accountNumber, fromStr, toStr, grouping, schema, propertyId, mpxn);

        return result;
    }

    private async Task<JsonDocument> ExecuteCostOfUsageQueryAsync(
        OctopusAccountSettings settings, string accountNumber, string fromStr, string toStr, string grouping, CostOfUsageSchema schema, int? propertyId = null, string? mpxn = null)
    {
        var variables = new Dictionary<string, object?>();
        if (schema.ArgNames.Contains("accountNumber"))
            variables["accountNumber"] = accountNumber;
        if (schema.StartArg is not null)
            variables[schema.StartArg] = fromStr;
        if (schema.EndArg is not null)
            variables[schema.EndArg] = toStr;
        if (schema.ArgNames.Contains("grouping"))
            variables["grouping"] = grouping;
        if (schema.ArgNames.Contains("propertyId") && propertyId.HasValue)
            variables["propertyId"] = propertyId.Value;
        if (schema.ArgNames.Contains("mpxn") && mpxn is not null)
            variables["mpxn"] = mpxn;

        var varDeclarations = new List<string>();
        if (variables.ContainsKey("accountNumber"))
            varDeclarations.Add("$accountNumber: String!");
        if (schema.StartArg is not null)
            varDeclarations.Add($"${schema.StartArg}: DateTime!");
        if (schema.EndArg is not null)
            varDeclarations.Add($"${schema.EndArg}: DateTime!");
        if (variables.ContainsKey("grouping"))
            varDeclarations.Add("$grouping: ConsumptionGroupings!");
        if (variables.ContainsKey("propertyId"))
            varDeclarations.Add("$propertyId: Int!");
        if (variables.ContainsKey("mpxn"))
            varDeclarations.Add("$mpxn: String!");

        string fieldSelection;
        if (schema.IsConnection && schema.FieldNames.Contains("edges"))
        {
            // Build edge node fields, filtered against the discovered node schema
            var wantedNodeFields = new[] { "startAt", "endAt", "fromDatetime", "toDatetime",
                "costInclTax", "costExclTax", "consumptionKwh",
                "unitRateInclTax", "unitRateExclTax", "unitRate",
                "costCurrency", "standingCharge", "totalCost", "totalConsumption" };
            var availableNodeFields = schema.NodeFieldNames.Count > 0
                ? schema.NodeFieldNames.Where(f => wantedNodeFields.Contains(f)).ToList()
                : wantedNodeFields.ToList();
            if (availableNodeFields.Count == 0)
                availableNodeFields = schema.NodeFieldNames.Count > 0 ? schema.NodeFieldNames : wantedNodeFields.ToList();
            var edgeNodeFields = string.Join(" ", availableNodeFields);
            fieldSelection = $"edges {{ node {{ {edgeNodeFields} }} }} pageInfo {{ hasNextPage endCursor }}";

            // Add pagination args to the query ONLY if the API supports them
            if (schema.ArgNames.Contains("first"))
            {
                variables["first"] = 100;
                varDeclarations.Add("$first: Int");
            }
            if (schema.ArgNames.Contains("after"))
            {
                variables["after"] = (string?)null;
                varDeclarations.Add("$after: String");
            }
        }
        else
        {
            var wantedFields = new[] { "startAt", "endAt", "fromDatetime", "toDatetime",
                "costInclTax", "costExclTax", "consumptionKwh",
                "unitRateInclTax", "unitRateExclTax", "unitRate",
                "costCurrency", "standingCharge", "totalCost", "totalConsumption" };
            var availableFields = schema.FieldNames.Where(f => wantedFields.Contains(f)).ToList();
            if (availableFields.Count == 0)
                availableFields = schema.FieldNames;
            fieldSelection = string.Join(" ", availableFields);
        }

        // Build args from variables AFTER pagination args have been added
        var args = variables.Keys.Select(k => $"{k}: ${k}");

        var query = $$"""
        query CostOfUsage({{string.Join(", ", varDeclarations)}}) {
          costOfUsage({{string.Join(", ", args)}}) {
            {{fieldSelection}}
          }
        }
        """;

        if (schema.IsConnection)
        {
            var allEdges = new List<JsonElement>();
            string? cursor = null;

            while (true)
            {
                if (schema.ArgNames.Contains("after"))
                    variables["after"] = cursor;

                var result = await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(variables));
                if (result.RootElement.TryGetProperty("errors", out _))
                    return result;

                var data = result.RootElement.GetProperty("data");
                if (!data.TryGetProperty("costOfUsage", out var costOfUsage)
                    || costOfUsage.ValueKind == JsonValueKind.Null)
                    return result;

                if (costOfUsage.TryGetProperty("edges", out var edges))
                    foreach (var edge in edges.EnumerateArray())
                        allEdges.Add(edge.Clone());

                var hasNext = costOfUsage.TryGetProperty("pageInfo", out var pi)
                    && pi.TryGetProperty("hasNextPage", out var hn) && hn.GetBoolean();
                if (!hasNext) break;

                cursor = pi.GetProperty("endCursor").GetString();
                result.Dispose();
            }

            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("data");
                writer.WriteStartObject();
                writer.WritePropertyName("costOfUsage");
                writer.WriteStartObject();
                writer.WritePropertyName("edges");
                writer.WriteStartArray();
                foreach (var edge in allEdges) edge.WriteTo(writer);
                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            return JsonDocument.Parse(ms.ToArray());
        }
        else
        {
            var result = await ExecuteRawQueryAsync(settings, query, JsonSerializer.SerializeToElement(variables));
            if (result.RootElement.TryGetProperty("errors", out _))
                return result;

            var data = result.RootElement.GetProperty("data");
            if (!data.TryGetProperty("costOfUsage", out var costOfUsage)
                || costOfUsage.ValueKind == JsonValueKind.Null)
                return result;

            // Normalise into edges/node format for consistent downstream parsing
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("data");
                writer.WriteStartObject();
                writer.WritePropertyName("costOfUsage");
                writer.WriteStartObject();
                writer.WritePropertyName("edges");
                writer.WriteStartArray();

                if (costOfUsage.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in costOfUsage.EnumerateArray())
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName("node");
                        item.WriteTo(writer);
                        writer.WriteEndObject();
                    }
                }
                else if (costOfUsage.ValueKind == JsonValueKind.Object)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("node");
                    costOfUsage.WriteTo(writer);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            return JsonDocument.Parse(ms.ToArray());
        }
    }

    // ── Raw / Generic ────────────────────────────────────────────────

    /// <summary>
    /// Executes an arbitrary GraphQL query with optional variables.
    /// Used by the /graphql pass-through endpoint and by parameterised queries above.
    /// </summary>
    public async Task<JsonDocument> ExecuteRawQueryAsync(OctopusAccountSettings settings, string query, JsonElement? variables = null)
    {
        var payload = variables.HasValue
            ? JsonSerializer.Serialize(new { query, variables = variables.Value })
            : JsonSerializer.Serialize(new { query });

        return await ExecuteQueryAsync(settings, payload);
    }

    // ── Transport ────────────────────────────────────────────────────

    private async Task<JsonDocument> ExecuteQueryAsync(OctopusAccountSettings settings, string query)
    {
        const int maxRetries = 3;
        var cacheKey = GetCacheKey(settings);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var token = await GetAuthTokenAsync(settings);

            using var request = new HttpRequestMessage(HttpMethod.Post, "");
            request.Headers.TryAddWithoutValidation("Authorization", token);
            request.Content = new StringContent(query, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return JsonDocument.Parse(result);

            if ((int)response.StatusCode == 403 && attempt < maxRetries)
            {
                // 403 from the CDN/WAF is typically transient rate-limiting.
                // Evict the cached token and retry after a backoff.
                _logger.LogWarning(
                    "Octopus API returned 403 (attempt {Attempt}/{Max}), evicting token and retrying after backoff. Response: {Response}",
                    attempt + 1, maxRetries, result);

                TokenCache.TryRemove(cacheKey, out _);

                var delay = TimeSpan.FromSeconds(2 * (attempt + 1)); // 2s, 4s, 6s
                await Task.Delay(delay);
                continue;
            }

            throw new HttpRequestException($"Octopus API returned {(int)response.StatusCode}: {result}");
        }

        throw new HttpRequestException("Octopus API request failed after all retry attempts");
    }
}
