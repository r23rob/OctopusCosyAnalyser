using System.Globalization;
using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.SecretProtection;

namespace OctopusCosyAnalyser.ApiService.Data;

/// <summary>
/// DynamoDB single-table implementation of <see cref="ICosyDataStore"/>.
///
/// Table: cosydays (configurable via DYNAMODB_TABLE_NAME)
/// Keys: PK (String) + SK (String)
///
/// Entity key schema:
///   Settings       PK=OWNER#{ownerId}       SK=SETTINGS#{accountNumber}
///   Device         PK=OWNER#{ownerId}       SK=DEVICE#{deviceId}
///   Device registry PK=ACTIVE_DEVICES       SK={ownerId}#{deviceId}
///   Snapshot       PK=DEVICE#{deviceId}     SK=SNAP#{iso8601}
///   Consumption    PK=DEVICE#{deviceId}     SK=CONS#{iso8601}
///   TimeSeries     PK=DEVICE#{deviceId}     SK=TS#{iso8601}
///   EnergyInterval PK=DEVICE#{deviceId}     SK=INTV#{iso8601}
///   DailyCost      PK=DEVICE#{deviceId}     SK=COST#{yyyy-MM-dd}
///   TariffRate     PK=DEVICE#{deviceId}     SK=RATE#{iso8601}
///   DataProtection PK=SYSTEM                SK=DPKEY#{keyId}
///
/// Thread-safe: IAmazonDynamoDB is itself thread-safe; this class holds no mutable state.
/// </summary>
public sealed class DynamoDataStore : ICosyDataStore
{
    private readonly IAmazonDynamoDB _db;
    private readonly ISecretProtector _protector;
    private readonly string _table;

    public DynamoDataStore(IAmazonDynamoDB db, ISecretProtector protector)
    {
        _db = db;
        _protector = protector;
        _table = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? "cosydays";
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static string Iso(DateTime dt) => dt.ToString("o", CultureInfo.InvariantCulture);
    private static string DateStr(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static AttributeValue S(string v) => new() { S = v };
    private static AttributeValue N(decimal v) => new() { N = v.ToString(CultureInfo.InvariantCulture) };
    private static AttributeValue BOOL(bool v) => new() { BOOL = v };

    private static string? GetS(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.S is not null ? v.S : null;

    private static decimal? GetN(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.N is not null
            ? decimal.Parse(v.N, CultureInfo.InvariantCulture)
            : null;

    private static bool? GetBool(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.IsBOOLSet ? v.BOOL : null;

    private static DateTime? GetDt(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.S is not null
            ? DateTime.Parse(v.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    private static DateOnly? GetDate(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.S is not null
            ? DateOnly.ParseExact(v.S, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;

    private static int? GetInt(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.N is not null
            ? int.Parse(v.N, CultureInfo.InvariantCulture)
            : null;

    private static short GetShort(Dictionary<string, AttributeValue> item, string key)
        => item.TryGetValue(key, out var v) && v.N is not null
            ? short.Parse(v.N, CultureInfo.InvariantCulture)
            : (short)0;

    private static void PutS(Dictionary<string, AttributeValue> item, string key, string? value)
    {
        if (value is not null) item[key] = S(value);
    }

    private static void PutN(Dictionary<string, AttributeValue> item, string key, decimal? value)
    {
        if (value.HasValue) item[key] = N(value.Value);
    }

    private static void PutBool(Dictionary<string, AttributeValue> item, string key, bool? value)
    {
        if (value.HasValue) item[key] = BOOL(value.Value);
    }

    private static void PutDt(Dictionary<string, AttributeValue> item, string key, DateTime? value)
    {
        if (value.HasValue) item[key] = S(Iso(value.Value));
    }

    private static void PutDate(Dictionary<string, AttributeValue> item, string key, DateOnly? value)
    {
        if (value.HasValue) item[key] = S(DateStr(value.Value));
    }

    private static void PutInt(Dictionary<string, AttributeValue> item, string key, int? value)
    {
        if (value.HasValue) item[key] = new AttributeValue { N = value.Value.ToString(CultureInfo.InvariantCulture) };
    }

    private static string EncodeCursor(Dictionary<string, AttributeValue> lastKey)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            lastKey.ToDictionary(kv => kv.Key, kv => kv.Value.S ?? kv.Value.N ?? ""))));

    private static Dictionary<string, AttributeValue>? DecodeCursor(string? cursor, string pkName = "PK", string skName = "SK")
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(
            Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
        return dict?.ToDictionary(kv => kv.Key, kv => new AttributeValue { S = kv.Value });
    }

    /// <summary>Runs a paginated query and collects all items.</summary>
    private async Task<List<Dictionary<string, AttributeValue>>> QueryAllAsync(
        QueryRequest request, CancellationToken ct)
    {
        var all = new List<Dictionary<string, AttributeValue>>();
        Dictionary<string, AttributeValue>? lastKey = null;

        do
        {
            request.ExclusiveStartKey = lastKey;
            var resp = await _db.QueryAsync(request, ct).ConfigureAwait(false);
            all.AddRange(resp.Items);
            lastKey = resp.LastEvaluatedKey is { Count: > 0 } ? resp.LastEvaluatedKey : null;
        } while (lastKey is not null);

        return all;
    }

    /// <summary>BatchWriteItem in chunks of 25.</summary>
    private async Task BatchWriteAsync(List<WriteRequest> requests, CancellationToken ct)
    {
        for (var i = 0; i < requests.Count; i += 25)
        {
            var chunk = requests.GetRange(i, Math.Min(25, requests.Count - i));
            await _db.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [_table] = chunk
                }
            }, ct).ConfigureAwait(false);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Settings   PK=OWNER#{ownerId}  SK=SETTINGS#{accountNumber}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<OctopusAccountSettings>> ListSettingsAsync(string ownerId, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"OWNER#{ownerId}"),
                [":skPrefix"] = S("SETTINGS#"),
            },
        }, ct);

        return items.Select(MapSettings).ToList();
    }

    public async Task<OctopusAccountSettings?> GetSettingsAsync(string ownerId, string accountNumber, CancellationToken ct = default)
    {
        var resp = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new()
            {
                ["PK"] = S($"OWNER#{ownerId}"),
                ["SK"] = S($"SETTINGS#{accountNumber}"),
            },
        }, ct).ConfigureAwait(false);

        return resp.Item is { Count: > 0 } ? MapSettings(resp.Item) : null;
    }

    public async Task UpsertSettingsAsync(OctopusAccountSettings settings, CancellationToken ct = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"OWNER#{settings.OwnerId}"),
            ["SK"] = S($"SETTINGS#{settings.AccountNumber}"),
            ["AccountNumber"] = S(settings.AccountNumber),
        };

        PutS(item, "OwnerId", settings.OwnerId);
        PutS(item, "ApiKey", _protector.Protect(settings.ApiKey));
        PutS(item, "Email", settings.Email);
        PutS(item, "OctopusPassword", _protector.Protect(settings.OctopusPassword));
        PutS(item, "AnthropicApiKey", _protector.Protect(settings.AnthropicApiKey));
        PutS(item, "AuthMode", settings.AuthMode);
        PutDt(item, "CreatedAt", settings.CreatedAt);
        PutDt(item, "UpdatedAt", settings.UpdatedAt);

        await _db.PutItemAsync(new PutItemRequest { TableName = _table, Item = item }, ct).ConfigureAwait(false);
    }

    private OctopusAccountSettings MapSettings(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        AccountNumber = GetS(item, "AccountNumber") ?? "",
        ApiKey = _protector.Unprotect(GetS(item, "ApiKey")) ?? "",
        Email = GetS(item, "Email"),
        OctopusPassword = _protector.Unprotect(GetS(item, "OctopusPassword")),
        AnthropicApiKey = _protector.Unprotect(GetS(item, "AnthropicApiKey")),
        AuthMode = GetS(item, "AuthMode") ?? "apikey",
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
        UpdatedAt = GetDt(item, "UpdatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Devices   PK=OWNER#{ownerId}  SK=DEVICE#{deviceId}
    //            PK=ACTIVE_DEVICES   SK={ownerId}#{deviceId}  (registry)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<HeatPumpDevice>> ListDevicesAsync(string ownerId, bool activeOnly = true, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"OWNER#{ownerId}"),
                [":skPrefix"] = S("DEVICE#"),
            },
        }, ct);

        var devices = items.Select(MapDevice).ToList();
        return activeOnly ? devices.Where(d => d.IsActive).ToList() : devices;
    }

    public async Task<HeatPumpDevice?> GetDeviceAsync(string ownerId, string deviceId, CancellationToken ct = default)
    {
        var resp = await _db.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new()
            {
                ["PK"] = S($"OWNER#{ownerId}"),
                ["SK"] = S($"DEVICE#{deviceId}"),
            },
        }, ct).ConfigureAwait(false);

        return resp.Item is { Count: > 0 } ? MapDevice(resp.Item) : null;
    }

    public async Task<HeatPumpDevice?> GetDeviceByAccountAsync(string ownerId, string accountNumber, CancellationToken ct = default)
    {
        var devices = await ListDevicesAsync(ownerId, activeOnly: false, ct);
        return devices.FirstOrDefault(d => d.AccountNumber == accountNumber);
    }

    public async Task UpsertDeviceAsync(HeatPumpDevice device, CancellationToken ct = default)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"OWNER#{device.OwnerId}"),
            ["SK"] = S($"DEVICE#{device.DeviceId}"),
            ["DeviceId"] = S(device.DeviceId),
            ["AccountNumber"] = S(device.AccountNumber),
            ["IsActive"] = BOOL(device.IsActive),
        };

        PutS(item, "OwnerId", device.OwnerId);
        PutS(item, "MeterSerialNumber", device.MeterSerialNumber);
        PutS(item, "Mpan", device.Mpan);
        PutS(item, "Euid", device.Euid);
        PutInt(item, "PropertyId", device.PropertyId);
        PutDt(item, "CreatedAt", device.CreatedAt);
        PutDt(item, "LastSyncAt", device.LastSyncAt);

        var requests = new List<WriteRequest>
        {
            new() { PutRequest = new PutRequest { Item = item } },
        };

        // Maintain the ACTIVE_DEVICES registry for cross-owner worker queries.
        if (device.IsActive)
        {
            var registryItem = new Dictionary<string, AttributeValue>
            {
                ["PK"] = S("ACTIVE_DEVICES"),
                ["SK"] = S($"{device.OwnerId}#{device.DeviceId}"),
                ["OwnerId"] = S(device.OwnerId ?? ""),
                ["DeviceId"] = S(device.DeviceId),
                ["AccountNumber"] = S(device.AccountNumber),
            };
            PutS(registryItem, "MeterSerialNumber", device.MeterSerialNumber);
            PutS(registryItem, "Mpan", device.Mpan);
            PutS(registryItem, "Euid", device.Euid);
            PutInt(registryItem, "PropertyId", device.PropertyId);
            PutDt(registryItem, "CreatedAt", device.CreatedAt);
            PutDt(registryItem, "LastSyncAt", device.LastSyncAt);

            requests.Add(new WriteRequest { PutRequest = new PutRequest { Item = registryItem } });
        }
        else
        {
            // Remove from registry when deactivated.
            requests.Add(new WriteRequest
            {
                DeleteRequest = new DeleteRequest
                {
                    Key = new()
                    {
                        ["PK"] = S("ACTIVE_DEVICES"),
                        ["SK"] = S($"{device.OwnerId}#{device.DeviceId}"),
                    }
                }
            });
        }

        await BatchWriteAsync(requests, ct);
    }

    public async Task<List<HeatPumpDevice>> ListAllActiveDevicesAsync(CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S("ACTIVE_DEVICES"),
            },
        }, ct);

        return items.Select(MapDevice).ToList();
    }

    private static HeatPumpDevice MapDevice(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        AccountNumber = GetS(item, "AccountNumber") ?? "",
        MeterSerialNumber = GetS(item, "MeterSerialNumber"),
        Mpan = GetS(item, "Mpan"),
        Euid = GetS(item, "Euid"),
        PropertyId = GetInt(item, "PropertyId"),
        IsActive = GetBool(item, "IsActive") ?? true,
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
        LastSyncAt = GetDt(item, "LastSyncAt"),
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Snapshots   PK=DEVICE#{deviceId}  SK=SNAP#{iso8601}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<HeatPumpSnapshot>> GetSnapshotsAsync(
        string deviceId, DateTime from, DateTime to, string? cursor = null, int limit = 10000, CancellationToken ct = default)
    {
        var request = new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"SNAP#{Iso(from)}"),
                [":skTo"] = S($"SNAP#{Iso(to)}"),
            },
            ScanIndexForward = false,
            Limit = limit,
            ExclusiveStartKey = DecodeCursor(cursor),
        };

        var resp = await _db.QueryAsync(request, ct).ConfigureAwait(false);
        var items = resp.Items.Select(MapSnapshot).ToList();
        var nextCursor = resp.LastEvaluatedKey is { Count: > 0 } ? EncodeCursor(resp.LastEvaluatedKey) : null;

        return new PagedResult<HeatPumpSnapshot>(items, -1, nextCursor);
    }

    public async Task<DateTime?> GetLatestSnapshotTimeAsync(string deviceId, CancellationToken ct = default)
    {
        var resp = await _db.QueryAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skPrefix"] = S("SNAP#"),
            },
            ScanIndexForward = false,
            Limit = 1,
        }, ct).ConfigureAwait(false);

        if (resp.Items.Count == 0) return null;
        return GetDt(resp.Items[0], "SnapshotTakenAt");
    }

    public async Task PutSnapshotAsync(HeatPumpSnapshot snapshot, CancellationToken ct = default)
    {
        var item = SerializeSnapshot(snapshot);
        await _db.PutItemAsync(new PutItemRequest { TableName = _table, Item = item }, ct).ConfigureAwait(false);
    }

    public async Task PutSnapshotBatchAsync(List<HeatPumpSnapshot> snapshots, CancellationToken ct = default)
    {
        var requests = snapshots.Select(s => new WriteRequest
        {
            PutRequest = new PutRequest { Item = SerializeSnapshot(s) }
        }).ToList();

        await BatchWriteAsync(requests, ct);
    }

    public async Task<List<HeatPumpSnapshot>> GetSnapshotListAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"SNAP#{Iso(from)}"),
                [":skTo"] = S($"SNAP#{Iso(to)}"),
            },
            ScanIndexForward = false,
        }, ct);

        return items.Select(MapSnapshot).ToList();
    }

    private Dictionary<string, AttributeValue> SerializeSnapshot(HeatPumpSnapshot s)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"DEVICE#{s.DeviceId}"),
            ["SK"] = S($"SNAP#{Iso(s.SnapshotTakenAt)}"),
        };

        PutS(item, "OwnerId", s.OwnerId);
        PutS(item, "DeviceId", s.DeviceId);
        PutS(item, "AccountNumber", s.AccountNumber);

        PutN(item, "COP", s.CoefficientOfPerformance);
        PutN(item, "OutdoorTempC", s.OutdoorTemperatureCelsius);
        PutN(item, "HeatOutputKw", s.HeatOutputKilowatt);
        PutN(item, "PowerInputKw", s.PowerInputKilowatt);

        PutN(item, "SeasonalCOP", s.SeasonalCoefficientOfPerformance);
        PutN(item, "LifetimeHeatOutKwh", s.LifetimeHeatOutputKwh);
        PutN(item, "LifetimeEnergyInKwh", s.LifetimeEnergyInputKwh);

        PutBool(item, "ControllerConnected", s.ControllerConnected);
        PutN(item, "PrimaryZoneSetpointC", s.PrimaryZoneSetpointCelsius);
        PutS(item, "PrimaryZoneMode", s.PrimaryZoneMode);
        PutBool(item, "PrimaryZoneHeatDemand", s.PrimaryZoneHeatDemand);
        PutN(item, "PrimarySensorTempC", s.PrimarySensorTemperatureCelsius);

        PutN(item, "HeatingZoneSetpointC", s.HeatingZoneSetpointCelsius);
        PutS(item, "HeatingZoneMode", s.HeatingZoneMode);
        PutBool(item, "HeatingZoneHeatDemand", s.HeatingZoneHeatDemand);

        PutN(item, "RoomTempC", s.RoomTemperatureCelsius);
        PutN(item, "RoomHumidityPct", s.RoomHumidityPercentage);
        PutS(item, "RoomSensorCode", s.RoomSensorCode);

        PutS(item, "FlowTempMode", s.FlowTempMode);
        PutN(item, "WCMinC", s.WeatherCompensationMinCelsius);
        PutN(item, "WCMaxC", s.WeatherCompensationMaxCelsius);
        PutN(item, "HeatingFlowTempC", s.HeatingFlowTemperatureCelsius);
        PutN(item, "HeatingFlowTempAllowMinC", s.HeatingFlowTempAllowableMinCelsius);
        PutN(item, "HeatingFlowTempAllowMaxC", s.HeatingFlowTempAllowableMaxCelsius);

        PutS(item, "ControllerState", s.ControllerState);

        PutN(item, "HWZoneSetpointC", s.HotWaterZoneSetpointCelsius);
        PutS(item, "HWZoneMode", s.HotWaterZoneMode);
        PutBool(item, "HWZoneHeatDemand", s.HotWaterZoneHeatDemand);

        PutS(item, "SensorReadingsJson", s.SensorReadingsJson);

        PutDt(item, "SnapshotTakenAt", s.SnapshotTakenAt);
        PutDt(item, "CreatedAt", s.CreatedAt);

        return item;
    }

    private static HeatPumpSnapshot MapSnapshot(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        AccountNumber = GetS(item, "AccountNumber") ?? "",

        CoefficientOfPerformance = GetN(item, "COP"),
        OutdoorTemperatureCelsius = GetN(item, "OutdoorTempC"),
        HeatOutputKilowatt = GetN(item, "HeatOutputKw"),
        PowerInputKilowatt = GetN(item, "PowerInputKw"),

        SeasonalCoefficientOfPerformance = GetN(item, "SeasonalCOP"),
        LifetimeHeatOutputKwh = GetN(item, "LifetimeHeatOutKwh"),
        LifetimeEnergyInputKwh = GetN(item, "LifetimeEnergyInKwh"),

        ControllerConnected = GetBool(item, "ControllerConnected"),
        PrimaryZoneSetpointCelsius = GetN(item, "PrimaryZoneSetpointC"),
        PrimaryZoneMode = GetS(item, "PrimaryZoneMode"),
        PrimaryZoneHeatDemand = GetBool(item, "PrimaryZoneHeatDemand"),
        PrimarySensorTemperatureCelsius = GetN(item, "PrimarySensorTempC"),

        HeatingZoneSetpointCelsius = GetN(item, "HeatingZoneSetpointC"),
        HeatingZoneMode = GetS(item, "HeatingZoneMode"),
        HeatingZoneHeatDemand = GetBool(item, "HeatingZoneHeatDemand"),

        RoomTemperatureCelsius = GetN(item, "RoomTempC"),
        RoomHumidityPercentage = GetN(item, "RoomHumidityPct"),
        RoomSensorCode = GetS(item, "RoomSensorCode"),

        FlowTempMode = GetS(item, "FlowTempMode"),
        WeatherCompensationMinCelsius = GetN(item, "WCMinC"),
        WeatherCompensationMaxCelsius = GetN(item, "WCMaxC"),
        HeatingFlowTemperatureCelsius = GetN(item, "HeatingFlowTempC"),
        HeatingFlowTempAllowableMinCelsius = GetN(item, "HeatingFlowTempAllowMinC"),
        HeatingFlowTempAllowableMaxCelsius = GetN(item, "HeatingFlowTempAllowMaxC"),

        ControllerState = GetS(item, "ControllerState"),

        HotWaterZoneSetpointCelsius = GetN(item, "HWZoneSetpointC"),
        HotWaterZoneMode = GetS(item, "HWZoneMode"),
        HotWaterZoneHeatDemand = GetBool(item, "HWZoneHeatDemand"),

        SensorReadingsJson = GetS(item, "SensorReadingsJson"),

        SnapshotTakenAt = GetDt(item, "SnapshotTakenAt") ?? DateTime.MinValue,
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Consumption   PK=DEVICE#{deviceId}  SK=CONS#{iso8601}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<PagedResult<ConsumptionReading>> GetConsumptionAsync(
        string deviceId, DateTime from, DateTime to, string? cursor = null, int limit = 10000, CancellationToken ct = default)
    {
        var request = new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"CONS#{Iso(from)}"),
                [":skTo"] = S($"CONS#{Iso(to)}"),
            },
            ScanIndexForward = false,
            Limit = limit,
            ExclusiveStartKey = DecodeCursor(cursor),
        };

        var resp = await _db.QueryAsync(request, ct).ConfigureAwait(false);
        var items = resp.Items.Select(MapConsumption).ToList();
        var nextCursor = resp.LastEvaluatedKey is { Count: > 0 } ? EncodeCursor(resp.LastEvaluatedKey) : null;

        return new PagedResult<ConsumptionReading>(items, -1, nextCursor);
    }

    public async Task<HashSet<DateTime>> GetConsumptionTimestampsAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"CONS#{Iso(from)}"),
                [":skTo"] = S($"CONS#{Iso(to)}"),
            },
            ProjectionExpression = "SK",
        }, ct);

        return items.Select(i =>
        {
            var sk = i["SK"].S;
            return DateTime.Parse(sk["CONS#".Length..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }).ToHashSet();
    }

    public async Task PutConsumptionBatchAsync(List<ConsumptionReading> readings, CancellationToken ct = default)
    {
        var requests = readings.Select(r => new WriteRequest
        {
            PutRequest = new PutRequest { Item = SerializeConsumption(r) }
        }).ToList();

        await BatchWriteAsync(requests, ct);
    }

    private static Dictionary<string, AttributeValue> SerializeConsumption(ConsumptionReading r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"DEVICE#{r.DeviceId}"),
            ["SK"] = S($"CONS#{Iso(r.ReadAt)}"),
            ["DeviceId"] = S(r.DeviceId),
            ["Consumption"] = N(r.Consumption),
        };

        PutS(item, "OwnerId", r.OwnerId);
        PutDt(item, "ReadAt", r.ReadAt);
        PutN(item, "ConsumptionDelta", r.ConsumptionDelta);
        PutN(item, "Demand", r.Demand);
        PutDt(item, "CreatedAt", r.CreatedAt);

        return item;
    }

    private static ConsumptionReading MapConsumption(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        ReadAt = GetDt(item, "ReadAt") ?? DateTime.MinValue,
        Consumption = GetN(item, "Consumption") ?? 0m,
        ConsumptionDelta = GetN(item, "ConsumptionDelta"),
        Demand = GetN(item, "Demand"),
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  TimeSeries   PK=DEVICE#{deviceId}  SK=TS#{iso8601}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<HeatPumpTimeSeriesRecord>> GetTimeSeriesAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"TS#{Iso(from)}"),
                [":skTo"] = S($"TS#{Iso(to)}"),
            },
        }, ct);

        return items.Select(MapTimeSeries).ToList();
    }

    public async Task<HashSet<DateTime>> GetTimeSeriesTimestampsAsync(string deviceId, DateTime from, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK >= :skFrom",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"TS#{Iso(from)}"),
            },
            ProjectionExpression = "SK",
        }, ct);

        return items.Select(i =>
        {
            var sk = i["SK"].S;
            return DateTime.Parse(sk["TS#".Length..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }).ToHashSet();
    }

    public async Task PutTimeSeriesBatchAsync(List<HeatPumpTimeSeriesRecord> records, CancellationToken ct = default)
    {
        var requests = records.Select(r => new WriteRequest
        {
            PutRequest = new PutRequest { Item = SerializeTimeSeries(r) }
        }).ToList();

        await BatchWriteAsync(requests, ct);
    }

    private static Dictionary<string, AttributeValue> SerializeTimeSeries(HeatPumpTimeSeriesRecord r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"DEVICE#{r.DeviceId}"),
            ["SK"] = S($"TS#{Iso(r.StartAt)}"),
            ["DeviceId"] = S(r.DeviceId),
        };

        PutS(item, "OwnerId", r.OwnerId);
        PutDt(item, "StartAt", r.StartAt);
        PutDt(item, "EndAt", r.EndAt);
        PutN(item, "EnergyInputKwh", r.EnergyInputKwh);
        PutN(item, "EnergyOutputKwh", r.EnergyOutputKwh);
        PutN(item, "OutdoorTempC", r.OutdoorTemperatureCelsius);
        PutDt(item, "CreatedAt", r.CreatedAt);

        return item;
    }

    private static HeatPumpTimeSeriesRecord MapTimeSeries(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        StartAt = GetDt(item, "StartAt") ?? DateTime.MinValue,
        EndAt = GetDt(item, "EndAt") ?? DateTime.MinValue,
        EnergyInputKwh = GetN(item, "EnergyInputKwh"),
        EnergyOutputKwh = GetN(item, "EnergyOutputKwh"),
        OutdoorTemperatureCelsius = GetN(item, "OutdoorTempC"),
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  DailyCost   PK=DEVICE#{deviceId}  SK=COST#{yyyy-MM-dd}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<DailyCostRecord>> GetDailyCostsAsync(string deviceId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"COST#{DateStr(from)}"),
                [":skTo"] = S($"COST#{DateStr(to)}"),
            },
        }, ct);

        return items.Select(MapDailyCost).ToList();
    }

    public async Task<Dictionary<DateOnly, DailyCostRecord>> GetDailyCostMapAsync(string deviceId, DateOnly from, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK >= :skFrom",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"COST#{DateStr(from)}"),
            },
        }, ct);

        return items
            .Select(MapDailyCost)
            .ToDictionary(r => r.Date);
    }

    public async Task<bool> HasAnyCostDataAsync(string deviceId, CancellationToken ct = default)
    {
        var resp = await _db.QueryAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skPrefix"] = S("COST#"),
            },
            Limit = 1,
            ProjectionExpression = "SK",
        }, ct).ConfigureAwait(false);

        return resp.Items.Count > 0;
    }

    public async Task UpsertDailyCostBatchAsync(List<DailyCostRecord> records, CancellationToken ct = default)
    {
        var requests = records.Select(r => new WriteRequest
        {
            PutRequest = new PutRequest { Item = SerializeDailyCost(r) }
        }).ToList();

        await BatchWriteAsync(requests, ct);
    }

    private static Dictionary<string, AttributeValue> SerializeDailyCost(DailyCostRecord r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"DEVICE#{r.DeviceId}"),
            ["SK"] = S($"COST#{DateStr(r.Date)}"),
            ["DeviceId"] = S(r.DeviceId),
            ["Date"] = S(DateStr(r.Date)),
            ["TotalCostPence"] = N(r.TotalCostPence),
            ["TotalUsageKwh"] = N(r.TotalUsageKwh),
            ["AvgUnitRatePence"] = N(r.AvgUnitRatePence),
        };

        PutS(item, "OwnerId", r.OwnerId);
        PutN(item, "StandingChargePence", r.StandingChargePence);
        PutDt(item, "CreatedAt", r.CreatedAt);
        PutDt(item, "UpdatedAt", r.UpdatedAt);

        return item;
    }

    private static DailyCostRecord MapDailyCost(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        Date = GetDate(item, "Date") ?? DateOnly.MinValue,
        TotalCostPence = GetN(item, "TotalCostPence") ?? 0m,
        TotalUsageKwh = GetN(item, "TotalUsageKwh") ?? 0m,
        AvgUnitRatePence = GetN(item, "AvgUnitRatePence") ?? 0m,
        StandingChargePence = GetN(item, "StandingChargePence"),
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
        UpdatedAt = GetDt(item, "UpdatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  TariffRate   PK=DEVICE#{deviceId}  SK=RATE#{iso8601}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<TariffRate>> GetTariffRatesAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"RATE#{Iso(from)}"),
                [":skTo"] = S($"RATE#{Iso(to)}"),
            },
        }, ct);

        return items.Select(MapTariffRate).ToList();
    }

    public async Task UpsertTariffRateBatchAsync(List<TariffRate> rates, CancellationToken ct = default)
    {
        var requests = rates.Select(r => new WriteRequest
        {
            PutRequest = new PutRequest { Item = SerializeTariffRate(r) }
        }).ToList();

        await BatchWriteAsync(requests, ct);
    }

    private static Dictionary<string, AttributeValue> SerializeTariffRate(TariffRate r)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"DEVICE#{r.DeviceId}"),
            ["SK"] = S($"RATE#{Iso(r.ValidFrom)}"),
            ["DeviceId"] = S(r.DeviceId),
            ["ValidFrom"] = S(Iso(r.ValidFrom)),
            ["UnitRatePence"] = N(r.UnitRatePence),
        };

        PutS(item, "OwnerId", r.OwnerId);
        PutDt(item, "ValidTo", r.ValidTo);
        PutDt(item, "CreatedAt", r.CreatedAt);

        return item;
    }

    private static TariffRate MapTariffRate(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        ValidFrom = GetDt(item, "ValidFrom") ?? DateTime.MinValue,
        ValidTo = GetDt(item, "ValidTo"),
        UnitRatePence = GetN(item, "UnitRatePence") ?? 0m,
        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  EnergyInterval   PK=DEVICE#{deviceId}  SK=INTV#{iso8601}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<EnergyInterval>> GetEnergyIntervalsAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :skFrom AND :skTo",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"INTV#{Iso(from)}"),
                [":skTo"] = S($"INTV#{Iso(to)}"),
            },
        }, ct);

        return items.Select(MapEnergyInterval).ToList();
    }

    public async Task<Dictionary<DateTime, EnergyInterval>> GetEnergyIntervalMapAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var list = await GetEnergyIntervalsAsync(deviceId, from, to, ct);
        return list.ToDictionary(i => i.IntervalStart);
    }

    public async Task<bool> HasAnyIntervalsAsync(string deviceId, CancellationToken ct = default)
    {
        var resp = await _db.QueryAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skPrefix"] = S("INTV#"),
            },
            Limit = 1,
            ProjectionExpression = "SK",
        }, ct).ConfigureAwait(false);

        return resp.Items.Count > 0;
    }

    public async Task<DateTime?> GetLatestIntervalStartAsync(string deviceId, CancellationToken ct = default)
    {
        var resp = await _db.QueryAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skPrefix"] = S("INTV#"),
            },
            ScanIndexForward = false,
            Limit = 1,
        }, ct).ConfigureAwait(false);

        if (resp.Items.Count == 0) return null;
        return GetDt(resp.Items[0], "IntervalStart");
    }

    public async Task UpsertEnergyIntervalBatchAsync(List<EnergyInterval> intervals, CancellationToken ct = default)
    {
        var requests = intervals.Select(i => new WriteRequest
        {
            PutRequest = new PutRequest { Item = SerializeEnergyInterval(i) }
        }).ToList();

        await BatchWriteAsync(requests, ct);
    }

    public async Task<List<EnergyInterval>> GetNullCostIntervalsAsync(string deviceId, DateTime from, CancellationToken ct = default)
    {
        // Query all intervals from 'from' onward and filter client-side for null cost.
        // DynamoDB can't filter on attribute absence efficiently (no GSI for this).
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND SK >= :skFrom",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S($"DEVICE#{deviceId}"),
                [":skFrom"] = S($"INTV#{Iso(from)}"),
            },
        }, ct);

        return items
            .Select(MapEnergyInterval)
            .Where(i => i.CostPence is null)
            .ToList();
    }

    public async Task UpdateEnergyIntervalBatchAsync(List<EnergyInterval> intervals, CancellationToken ct = default)
    {
        // Same as upsert for DynamoDB (PutItem is an upsert).
        await UpsertEnergyIntervalBatchAsync(intervals, ct);
    }

    private static Dictionary<string, AttributeValue> SerializeEnergyInterval(EnergyInterval i)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"] = S($"DEVICE#{i.DeviceId}"),
            ["SK"] = S($"INTV#{Iso(i.IntervalStart)}"),
            ["DeviceId"] = S(i.DeviceId),
        };

        PutS(item, "OwnerId", i.OwnerId);
        PutDt(item, "IntervalStart", i.IntervalStart);
        PutDt(item, "IntervalEnd", i.IntervalEnd);

        PutN(item, "ConsumptionKwh", i.ConsumptionKwh);
        PutN(item, "DemandW", i.DemandW);
        PutN(item, "HeatOutputKwh", i.HeatOutputKwh);
        PutN(item, "AvgCop", i.AvgCop);
        PutN(item, "AvgPowerInputKw", i.AvgPowerInputKw);
        PutN(item, "AvgOutdoorTempC", i.AvgOutdoorTempC);
        PutN(item, "AvgRoomTempC", i.AvgRoomTempC);
        PutN(item, "AvgFlowTempC", i.AvgFlowTempC);
        PutBool(item, "WasHeating", i.WasHeating);
        PutBool(item, "WasHotWater", i.WasHotWater);
        item["SnapshotCount"] = new AttributeValue { N = i.SnapshotCount.ToString(CultureInfo.InvariantCulture) };

        PutN(item, "UnitRatePencePerKwh", i.UnitRatePencePerKwh);
        PutN(item, "StandingChargePence", i.StandingChargePence);
        PutN(item, "CostPence", i.CostPence);

        PutDt(item, "CreatedAt", i.CreatedAt);
        PutDt(item, "UpdatedAt", i.UpdatedAt);

        return item;
    }

    private static EnergyInterval MapEnergyInterval(Dictionary<string, AttributeValue> item) => new()
    {
        OwnerId = GetS(item, "OwnerId"),
        DeviceId = GetS(item, "DeviceId") ?? "",
        IntervalStart = GetDt(item, "IntervalStart") ?? DateTime.MinValue,
        IntervalEnd = GetDt(item, "IntervalEnd") ?? DateTime.MinValue,

        ConsumptionKwh = GetN(item, "ConsumptionKwh"),
        DemandW = GetN(item, "DemandW"),
        HeatOutputKwh = GetN(item, "HeatOutputKwh"),
        AvgCop = GetN(item, "AvgCop"),
        AvgPowerInputKw = GetN(item, "AvgPowerInputKw"),
        AvgOutdoorTempC = GetN(item, "AvgOutdoorTempC"),
        AvgRoomTempC = GetN(item, "AvgRoomTempC"),
        AvgFlowTempC = GetN(item, "AvgFlowTempC"),
        WasHeating = GetBool(item, "WasHeating"),
        WasHotWater = GetBool(item, "WasHotWater"),
        SnapshotCount = GetShort(item, "SnapshotCount"),

        UnitRatePencePerKwh = GetN(item, "UnitRatePencePerKwh"),
        StandingChargePence = GetN(item, "StandingChargePence"),
        CostPence = GetN(item, "CostPence"),

        CreatedAt = GetDt(item, "CreatedAt") ?? DateTime.MinValue,
        UpdatedAt = GetDt(item, "UpdatedAt") ?? DateTime.MinValue,
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  Standing charges (for EnergyIntervalWorker)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Dictionary<DateOnly, decimal?>> GetStandingChargesAsync(string deviceId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var costs = await GetDailyCostsAsync(deviceId, from, to, ct);
        return costs.ToDictionary(c => c.Date, c => c.StandingChargePence);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DataProtection   PK=SYSTEM  SK=DPKEY#{keyId}
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<List<DataProtectionKey>> GetDataProtectionKeysAsync(CancellationToken ct = default)
    {
        var items = await QueryAllAsync(new QueryRequest
        {
            TableName = _table,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new()
            {
                [":pk"] = S("SYSTEM"),
                [":skPrefix"] = S("DPKEY#"),
            },
        }, ct);

        return items.Select(i => new DataProtectionKey
        {
            FriendlyName = GetS(i, "FriendlyName") ?? "",
            Xml = GetS(i, "Xml") ?? "",
        }).ToList();
    }

    public async Task PutDataProtectionKeyAsync(DataProtectionKey key, CancellationToken ct = default)
    {
        await _db.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = S("SYSTEM"),
                ["SK"] = S($"DPKEY#{key.FriendlyName}"),
                ["FriendlyName"] = S(key.FriendlyName),
                ["Xml"] = S(key.Xml),
            },
        }, ct).ConfigureAwait(false);
    }
}
