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

    private async Task<string> GetAuthTokenAsync(string email, string password)
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

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("https://api.octopus.energy/v1/graphql/", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(result);
        var token = json.RootElement.GetProperty("data").GetProperty("obtainKrakenToken").GetProperty("token").GetString();

            var result = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(result);
            var token = json.RootElement.GetProperty("data").GetProperty("obtainKrakenToken").GetProperty("token").GetString();

        var entry = new TokenCacheEntry(token, DateTime.UtcNow.AddMinutes(55));
        TokenCache[email] = entry;

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
    public async Task<JsonDocument> GetAccountAsync(string email, string password, string accountNumber)
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

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(new { accountNumber }));
    }

    /// <summary>
    /// Gets account properties and occupierEuids (needed to find the heat pump EUID).
    /// </summary>
    public async Task<JsonDocument> GetViewerPropertiesAsync(string email, string password)
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

        return await ExecuteRawQueryAsync(email, password, query);
    }

    /// <summary>
    /// Gets EUIDs directly from the heat pump controller API. Fallback when viewer query doesn't return EUIDs.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpControllerEuidsAsync(string email, string password, string accountNumber)
    {
        ValidateIdentifier(accountNumber, nameof(accountNumber));

        var query = """
        query GetEuids($accountNumber: String!) {
          heatPumpControllerEuids(accountNumber: $accountNumber)
        }
        """;

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(new { accountNumber }));
    }

    /// <summary>
    /// Gets heat pump device info (serial, make, model) by property ID.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpDeviceAsync(string email, string password, string accountNumber, int propertyId)
    {
        ValidateIdentifier(accountNumber, nameof(accountNumber));

        var query = """
        query GetHeatPumpDevice($accountNumber: String!, $propertyId: Int!) {
          heatPumpDevice(accountNumber: $accountNumber, propertyId: $propertyId) {
            id serialNumber make model installationDate
          }
        }
        """;

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(new { accountNumber, propertyId }));
    }

    /// <summary>
    /// Gets viewer properties including heat pump device details and EUIDs.
    /// NOTE: This queries the viewer/properties, not the heatPumpControllerConfiguration API.
    /// </summary>
    public async Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(string email, string password)
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

        return await ExecuteRawQueryAsync(email, password, query);
    }

    // ── Smart Meter ──────────────────────────────────────────────────

    /// <summary>
    /// Gets live smart meter telemetry: consumption, consumptionDelta, demand.
    /// </summary>
    public async Task<JsonDocument> GetSmartMeterTelemetryAsync(string email, string password, string deviceId)
    {
        ValidateIdentifier(deviceId, nameof(deviceId));

        var query = """
        query GetSmartMeterTelemetry($deviceId: String!) {
          smartMeterTelemetry(deviceId: $deviceId) {
            readAt consumption consumptionDelta demand
          }
        }
        """;

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(new { deviceId }));
    }

    /// <summary>
    /// Gets historical half-hourly consumption data via REST API (Basic auth, not GraphQL).
    /// Follows pagination automatically to retrieve all results across all pages.
    /// </summary>
    public async Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var fromStr = from.ToString(Constants.OctopusDateFormatSimple);
        var toStr = to.ToString(Constants.OctopusDateFormatSimple);

        var allResults = new List<JsonElement>();
        string? url = $"https://api.octopus.energy/v1/electricity-meter-points/{mpan}/meters/{serialNumber}/consumption/?period_from={fromStr}&period_to={toStr}&page_size={Constants.MaxConsumptionPageSize}";
        var authHeader = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiKey}:")));

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = authHeader;

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
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
    /// Uses the older heatPumpStatus query on api.octopus.energy (doesn't require EUID).
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpStatusAsync(string email, string password)
    {
        ValidateIdentifier(accountNumber, nameof(accountNumber));

        var query = """
        query GetHeatPumpStatus($accountNumber: String!) {
          heatPumpStatus(accountNumber: $accountNumber) {
            isConnected
            climateControlStatus {
              climateControlEnabled
              targetClimateControlTemperature
              currentClimateControlTemperature
            }
            waterTemperatureStatus {
              waterTemperatureEnabled
              currentWaterTemperature
            }
          }
        }
        """;

        return await ExecuteRawQueryAsync(email, password, query);
    }

    /// <summary>
    /// Lists available heat pump makes and models.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpVariantsAsync(string email, string password, string? make = null)
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

            return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(new { make }));
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

            return await ExecuteRawQueryAsync(email, password, query);
        }
    }

    // ── Heat Pump – Full Live Data (Primary Workhorse) ───────────────

    /// <summary>
    /// PRIMARY QUERY — batches 4 GraphQL queries in one call:
    ///   1. heatPumpControllerStatus      — sensors (temp, humidity, rssi, voltage, connectivity), zone telemetry
    ///   2. heatPumpControllerConfiguration — controller state + firmware, heat pump details (fault codes, flow temps,
    ///      weather comp with allowable ranges, quieter mode), zone config + schedules
    ///   3. heatPumpTimeSeriesPerformance (LIVE) — recent energy in/out, outdoor temp (COP computed client-side)
    ///   4. heatPumpLifetimePerformance   — seasonal COP, lifetime energy totals
    /// Used by /summary endpoint and the HeatPumpSnapshotWorker.
    /// Requests all available fields from the Octopus API schema.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(string email, string password, string accountNumber, string euid)
    {
        var now = DateTime.UtcNow;
        var liveStartAt = now.AddMinutes(-30).ToString(Constants.OctopusDateFormat);
        var liveEndAt = now.ToString(Constants.OctopusDateFormat);

        var query = """
        query HeatPumpStatusAndConfig($accountNumber: String!, $euid: ID!, $liveStartAt: DateTime!, $liveEndAt: DateTime!) {
          heatPumpControllerStatus(accountNumber: $accountNumber, euid: $euid) {
            sensors {
              code
              connectivity { online retrievedAt }
              telemetry { temperatureInCelsius humidityPercentage rssi voltage retrievedAt }
            }
            zones {
              zone
              telemetry { setpointInCelsius mode relaySwitchedOn heatDemand retrievedAt }
            }
          }
          heatPumpControllerConfiguration(accountNumber: $accountNumber, euid: $euid) {
            controller {
              accessPointPassword
              connected
              firmwareConfiguration { efr32 esp32 eui }
              heatPumpTimezone
              lastReset
              state
            }
            heatPump {
              faultCodes
              hasHeatPumpCompatibleCylinder
              hardwareVersion
              heatingFlowTemperature {
                allowableRange { maximum { unit value } minimum { unit value } }
                currentTemperature { unit value }
              }
              latestCounterReset
              manifoldEnabled
              maxWaterSetpoint
              minWaterSetpoint
              model
              quieterModeEnabled
              serialNumber
              weatherCompensation {
                allowableMaximumTemperatureRange { maximum { unit value } minimum { unit value } }
                allowableMinimumTemperatureRange { maximum { unit value } minimum { unit value } }
                currentRange { maximum { unit value } minimum { unit value } }
                enabled
              }
            }
            zones {
              configuration {
                callForHeat
                code
                currentOperation { action end mode setpointInCelsius }
                displayName
                emergency
                enabled
                heatDemand
                previousOperation { action mode setpointInCelsius }
                primarySensor
                sensors {
                  ... on ADCSensorConfiguration { code displayName enabled type }
                  ... on ZigbeeSensorConfiguration { id boostEnabled code displayName firmwareVersion type }
                }
                zoneType
              }
              schedules {
                days
                settings { action setpointInCelsius startTime zoneState }
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

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(variables));
    }

    // ── Heat Pump – Historic Performance ─────────────────────────────

    /// <summary>
    /// Gets aggregated performance for a date range (single totals, no time buckets).
    /// Returns: coefficientOfPerformance, energyOutput, energyInput.
    /// NOTE: Does NOT have a performanceGrouping parameter.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpTimeRangedPerformanceAsync(string email, string password, string accountNumber, string euid, DateTime from, DateTime to)
    {
        var fromStr = from.ToString(Constants.OctopusDateFormat);
        var toStr = to.ToString(Constants.OctopusDateFormat);

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

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(variables));
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
    public async Task<JsonDocument> GetHeatPumpTimeSeriesPerformanceAsync(string email, string password, string accountNumber, string euid, DateTime from, DateTime to, string? performanceGroupingOverride = null)
    {
        var fromStr = from.ToString(Constants.OctopusDateFormat);
        var toStr = to.ToString(Constants.OctopusDateFormat);

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

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(variables));
    }

    // ── Heat Pump – Controllers at Location ──────────────────────────

    /// <summary>
    /// Discovers all heat pump controllers at a given location.
    /// Useful for multi-HP setups and as a fallback during device discovery.
    /// </summary>
    public async Task<JsonDocument> GetHeatPumpControllersAtLocationAsync(string email, string password, string accountNumber, int propertyId)
    {
        var query = """
        query GetControllersAtLocation($accountNumber: String!, $propertyId: ID!) {
          heatPumpControllersAtLocation(accountNumber: $accountNumber, propertyId: $propertyId) {
            controller { euid }
            heatPumpModel
            location { propertyId }
            provisionedAt
          }
        }
        """;

        return await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(new { accountNumber, propertyId }));
    }

    // ── Tariff Rates ───────────────────────────────────────────────

    /// <summary>
    /// Gets applicable tariff rates for an account and meter point.
    /// Returns rate periods via Relay connection (edges/node pattern).
    /// </summary>
    public async Task<JsonDocument> GetApplicableRatesAsync(string email, string password, string accountNumber, string mpxn, DateTime startAt, DateTime endAt)
    {
        var startAtStr = startAt.ToString(Constants.OctopusDateFormat);
        var endAtStr = endAt.ToString(Constants.OctopusDateFormat);

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
                ["first"] = Constants.DefaultPageSize,
                ["after"] = cursor
            };

            var result = await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(variables));
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

    // Cached schema discovery for costOfUsage query (guarded by _schemaLock)
    private static CostOfUsageSchema? _costOfUsageSchema;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

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
        var kind = typeEl.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        var name = typeEl.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? "" : "";

        if (kind is "NON_NULL" or "LIST")
        {
            var isListLayer = kind == "LIST";
            if (typeEl.TryGetProperty("ofType", out var ofType))
            {
                var (innerName, innerIsArray, innerIsConnection) = UnwrapGraphQLType(ofType);
                return (innerName, innerIsArray || isListLayer, innerIsConnection);
            }
            return ("", isListLayer, false);
        }

        var isConnection = !string.IsNullOrEmpty(name) && name.Contains("Connection");
        return (name, false, isConnection);
    }

    /// <summary>
    /// Introspects a connection type to discover the node type's field names.
    /// Follows the chain: ConnectionType -> edges field -> EdgeType -> node field -> NodeType -> fields.
    /// </summary>
    private async Task<List<string>> DiscoverNodeTypeFieldsAsync(string email, string password, string connectionTypeName)
    {
        // Introspect the connection type to find the edges field's type
        var connIntrospection = await ExecuteRawQueryAsync(email, password, $$"""
            { __type(name: "{{connectionTypeName}}") { fields { name type { name kind ofType { name kind ofType { name kind } } } } } }
        """);

        var connData = connIntrospection.RootElement.GetProperty("data");
        if (!connData.TryGetProperty("__type", out var connType) || connType.ValueKind == JsonValueKind.Null)
            return [];

        // Find the 'edges' field and get its inner type
        string? edgeTypeName = null;
        foreach (var field in connType.GetProperty("fields").EnumerateArray())
        {
            if (field.GetProperty("name").GetString() == "edges")
            {
                var (typeName, _, _) = UnwrapGraphQLType(field.GetProperty("type"));
                edgeTypeName = typeName;
                break;
            }
        }

        if (string.IsNullOrEmpty(edgeTypeName))
            return [];

        // Introspect the edge type to find the 'node' field's type
        var edgeIntrospection = await ExecuteRawQueryAsync(email, password, $$"""
            { __type(name: "{{edgeTypeName}}") { fields { name type { name kind ofType { name kind ofType { name kind } } } } } }
        """);

        var edgeData = edgeIntrospection.RootElement.GetProperty("data");
        if (!edgeData.TryGetProperty("__type", out var edgeType) || edgeType.ValueKind == JsonValueKind.Null)
            return [];

        string? nodeTypeName = null;
        foreach (var field in edgeType.GetProperty("fields").EnumerateArray())
        {
            if (field.GetProperty("name").GetString() == "node")
            {
                var (typeName, _, _) = UnwrapGraphQLType(field.GetProperty("type"));
                nodeTypeName = typeName;
                break;
            }
        }

        if (string.IsNullOrEmpty(nodeTypeName))
            return [];

        // Introspect the node type to get its field names
        var nodeIntrospection = await ExecuteRawQueryAsync(email, password, $$"""
            { __type(name: "{{nodeTypeName}}") { fields { name } } }
        """);

        var nodeData = nodeIntrospection.RootElement.GetProperty("data");
        if (!nodeData.TryGetProperty("__type", out var nodeType) || nodeType.ValueKind == JsonValueKind.Null)
            return [];

        var nodeFields = new List<string>();
        foreach (var field in nodeType.GetProperty("fields").EnumerateArray())
            nodeFields.Add(field.GetProperty("name").GetString()!);

        _logger.LogInformation("Discovered node type {NodeType} with fields: [{Fields}]",
            nodeTypeName, string.Join(", ", nodeFields));

        return nodeFields;
    }

    private async Task<CostOfUsageSchema> DiscoverCostOfUsageSchemaAsync(string email, string password)
    {
        if (_costOfUsageSchema is not null)
            return _costOfUsageSchema;

        // Use deeper ofType nesting to handle NON_NULL(LIST(NON_NULL(Type))) patterns
        var introspectionQuery = """
        {
            "accountNumber", "startAt", "endAt", "grouping",
            "propertyId", "mpxn", "first", "after"
        };

        var result = await ExecuteRawQueryAsync(email, password, introspectionQuery);
        var fields = result.RootElement
            .GetProperty("data")
            .GetProperty("__schema")
            .GetProperty("queryType")
            .GetProperty("fields");

        JsonElement? costField = null;
        foreach (var field in fields.EnumerateArray())
        {
            "startAt", "endAt", "costInclTax", "costExclTax",
            "consumptionKwh", "unitRateInclTax", "unitRateExclTax",
            "standingCharge", "costCurrency"
        };

        var connectionFieldNames = new List<string> { "edges", "pageInfo" };

        var argNames = new List<string>();
        foreach (var arg in costField.Value.GetProperty("args").EnumerateArray())
            argNames.Add(arg.GetProperty("name").GetString()!);

        // Match date arguments — try specific names first, then fall back to substring matching
        var startDateArgs = new[] { "startAt", "fromDatetime", "from", "startDate", "periodFrom" };
        var endDateArgs = new[] { "endAt", "toDatetime", "to", "endDate", "periodTo" };
        var startArg = argNames.FirstOrDefault(a => startDateArgs.Contains(a));
        var endArg = argNames.FirstOrDefault(a => endDateArgs.Contains(a));

        // Fallback: look for any arg whose name contains start/from or end/to keywords
        if (startArg is null)
            startArg = argNames.FirstOrDefault(a =>
                a.Contains("start", StringComparison.OrdinalIgnoreCase) ||
                a.Contains("from", StringComparison.OrdinalIgnoreCase));
        if (endArg is null)
            endArg = argNames.FirstOrDefault(a =>
                !a.Equals("accountNumber", StringComparison.OrdinalIgnoreCase) &&
                (a.Contains("end", StringComparison.OrdinalIgnoreCase) ||
                 a.Contains("to", StringComparison.OrdinalIgnoreCase)));

        // Unwrap the return type (handles NON_NULL(LIST(NON_NULL(Type))) etc.)
        var typeEl = costField.Value.GetProperty("type");
        var (innerTypeName, isArray, isConnection) = UnwrapGraphQLType(typeEl);

        if (string.IsNullOrEmpty(innerTypeName))
            innerTypeName = "CostOfUsageType";

        // Introspect the return type's fields
        var typeIntrospection = await ExecuteRawQueryAsync(email, password, $$"""
            { __type(name: "{{innerTypeName}}") { name kind fields { name type { name kind ofType { name } } } } }
        """);

        var fieldNames = new List<string>();
        var typeData = typeIntrospection.RootElement.GetProperty("data");
        if (typeData.TryGetProperty("__type", out var introspectedType)
            && introspectedType.ValueKind != JsonValueKind.Null
            && introspectedType.TryGetProperty("fields", out var typeFields))
        {
            foreach (var f in typeFields.EnumerateArray())
            {
                var fieldName = f.GetProperty("name").GetString()!;
                fieldNames.Add(fieldName);
            }
        }

        // Only treat as a connection if the return type has BOTH edges AND pageInfo fields
        if (!isConnection)
            isConnection = fieldNames.Contains("edges") && fieldNames.Contains("pageInfo");

        // Introspect node-level fields for connection types
        var nodeFieldNames = fieldNames;
        if (isConnection)
        {
            try
            {
                // Find the edges field's inner type from the connection type introspection
                var edgesTypeName = await DiscoverNodeTypeFieldsAsync(email, password, innerTypeName);
                if (edgesTypeName.Count > 0)
                    nodeFieldNames = edgesTypeName;
                else
                    _logger.LogWarning("Could not discover node fields for connection type {TypeName}, will use all requested fields", innerTypeName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to introspect node type for {TypeName}, will use all requested fields", innerTypeName);
            }
        }

        _logger.LogInformation(
            "Discovered costOfUsage schema: args=[{Args}], startArg={StartArg}, endArg={EndArg}, " +
            "returnType={ReturnType}, isConnection={IsConnection}, fields=[{Fields}], nodeFields=[{NodeFields}]",
            string.Join(", ", argNames), startArg, endArg,
            innerTypeName, isConnection,
            string.Join(", ", fieldNames), string.Join(", ", nodeFieldNames));

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
        finally
        {
            _schemaLock.Release();
        }
    }

    /// <summary>
    /// Clears the cached costOfUsage schema so it will be re-discovered on next call.
    /// </summary>
    private static void InvalidateCostOfUsageSchemaCache() => _costOfUsageSchema = null;

    /// <summary>
    /// Gets the actual cost of energy usage for a date range.
    /// Uses a hardcoded schema definition (introspection is blocked by the Octopus backend API).
    /// grouping: HALF_HOUR, DAY, WEEK, MONTH, QUARTER
    /// </summary>
    public async Task<JsonDocument> GetCostOfUsageAsync(string email, string password, string accountNumber, DateTime from, DateTime to, string grouping = "DAY", int? propertyId = null, string? mpxn = null)
    {
        var fromStr = from.ToString(Constants.OctopusDateFormat);
        var toStr = to.ToString(Constants.OctopusDateFormat);

        var schema = await DiscoverCostOfUsageSchemaAsync(email, password);
        _logger.LogDebug("Cost of usage schema: args=[{Args}], startArg={Start}, endArg={End}, isConnection={IsConn}",
            string.Join(", ", schema.ArgNames), schema.StartArg, schema.EndArg, schema.IsConnection);

        JsonDocument result;
        try
        {
            result = await ExecuteCostOfUsageQueryAsync(email, password, accountNumber, fromStr, toStr, grouping, schema, propertyId, mpxn);
        }
        catch (HttpRequestException ex)
        {
            if (!ex.Message.Contains("400"))
                throw;

            // Schema may be stale - clear cache and retry with fresh introspection
            _logger.LogWarning(ex, "costOfUsage query returned 400, clearing schema cache and retrying");
            InvalidateCostOfUsageSchemaCache();
            schema = await DiscoverCostOfUsageSchemaAsync(email, password);
            result = await ExecuteCostOfUsageQueryAsync(email, password, accountNumber, fromStr, toStr, grouping, schema, propertyId, mpxn);
        }

        // Also handle GraphQL-level errors (some servers return 200 with errors array)
        if (result.RootElement.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            var firstError = errors[0].TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
            if (firstError.Contains("Unknown argument") || firstError.Contains("Cannot query field"))
            {
                _logger.LogWarning("costOfUsage query returned schema error: {Error}, clearing cache and retrying", firstError);
                InvalidateCostOfUsageSchemaCache();
                schema = await DiscoverCostOfUsageSchemaAsync(email, password);
                result.Dispose();
                result = await ExecuteCostOfUsageQueryAsync(email, password, accountNumber, fromStr, toStr, grouping, schema, propertyId, mpxn);
            }
        }

        return result;
    }

    private async Task<JsonDocument> ExecuteCostOfUsageQueryAsync(
        string email, string password, string accountNumber, string fromStr, string toStr, string grouping, CostOfUsageSchema schema, int? propertyId = null, string? mpxn = null)
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

                var result = await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(variables));
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
            var result = await ExecuteRawQueryAsync(email, password, query, JsonSerializer.SerializeToElement(variables));
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
    public async Task<JsonDocument> ExecuteRawQueryAsync(string email, string password, string query, JsonElement? variables = null)
    {
        var payload = variables.HasValue
            ? JsonSerializer.Serialize(new { query, variables = variables.Value })
            : JsonSerializer.Serialize(new { query });

        return await ExecuteQueryAsync(email, password, payload);
    }

    // ── Transport ────────────────────────────────────────────────────

    private async Task<JsonDocument> ExecuteQueryAsync(string email, string password, string query)
    {
        var token = await GetAuthTokenAsync(email, password);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var token = await GetAuthTokenAsync(settings);

        using var response = await _httpClient.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();

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
