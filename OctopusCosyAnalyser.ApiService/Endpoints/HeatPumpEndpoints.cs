using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class HeatPumpEndpoints
{
    public static void MapHeatPumpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/heatpump");

        static async Task<(HeatPumpDevice? Device, OctopusAccountSettings? Settings, IResult? Error)> GetDeviceAndSettingsAsync(
            CosyDbContext db, string deviceId)
        {
            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device is null)
                return (null, null, Results.NotFound("Device not found"));

            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber);

            if (settings is null)
                return (device, null, Results.Problem("Account settings not found. Save API key in /settings."));

            return (device, settings, null);
        }

        static async Task<(OctopusAccountSettings? Settings, IResult? Error)> GetSettingsForAccountAsync(
            CosyDbContext db, string accountNumber)
        {
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber);

            return settings is null
                ? (null, Results.Problem("Account settings not found. Save API key in /settings."))
                : (settings, null);
        }

        static JsonElement? FindAccount(JsonElement accounts, string accountNumber)
        {
            foreach (var acc in accounts.EnumerateArray())
            {
                if (acc.GetProperty("number").GetString() == accountNumber)
                    return acc;
            }

            return null;
        }

        // Get account info and set up device
        group.MapPost("/setup", async (string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, settingsError) = await GetSettingsForAccountAsync(db, accountNumber);
            if (settingsError is not null)
                return settingsError;

            // Get account data with electricity agreements
            var accountData = await client.GetAccountAsync(settings!.ApiKey, accountNumber);
            var data = accountData.RootElement.GetProperty("data").GetProperty("account");
            var agreements = data.GetProperty("electricityAgreements");

            if (agreements.GetArrayLength() == 0)
                return Results.NotFound("No active electricity agreements found");

            var meterPoint = agreements[0].GetProperty("meterPoint");
            var mpan = meterPoint.GetProperty("mpan").GetString()!;
            var meters = meterPoint.GetProperty("meters");

            if (meters.GetArrayLength() == 0)
                return Results.NotFound("No meters found for this agreement");

            var meter = meters[0];
            var serialNumber = meter.GetProperty("serialNumber").GetString()!;

            var smartDevices = meter.GetProperty("smartDevices");
            if (smartDevices.GetArrayLength() == 0)
                return Results.NotFound("No smart devices found");

            var deviceId = smartDevices[0].GetProperty("deviceId").GetString()!;

            // Get properties to find EUID using viewer query
            string? euid = null;
            int? propertyId = null;

            try
            {
                var viewerData = await client.GetViewerPropertiesAsync(settings.ApiKey);
                var viewer = viewerData.RootElement.GetProperty("data").GetProperty("viewer");
                var accounts = viewer.GetProperty("accounts");
                var account = FindAccount(accounts, accountNumber);

                if (account.HasValue)
                {
                    var properties = account.Value.GetProperty("properties");
                    if (properties.GetArrayLength() > 0)
                    {
                        var property = properties[0];
                        propertyId = property.GetProperty("id").GetInt32();

                        if (property.TryGetProperty("occupierEuids", out var euids) && euids.GetArrayLength() > 0)
                        {
                            euid = euids[0].GetString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // EUID fetch failed, continue without it
                System.Diagnostics.Debug.WriteLine($"Failed to fetch EUID: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(euid))
            {
                try
                {
                    var controllerEuids = await client.GetHeatPumpControllerEuidsAsync(settings.ApiKey, accountNumber);
                    if (controllerEuids.RootElement.TryGetProperty("data", out var controllerData)
                        && controllerData.TryGetProperty("octoHeatPumpControllerEuids", out var euids)
                        && euids.ValueKind == JsonValueKind.Array
                        && euids.GetArrayLength() > 0)
                    {
                        euid = euids[0].GetString();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch controller EUIDs: {ex.Message}");
                }
            }

            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.AccountNumber == accountNumber);
            if (device == null)
            {
                device = new HeatPumpDevice
                {
                    DeviceId = deviceId,
                    AccountNumber = accountNumber,
                    MeterSerialNumber = serialNumber,
                    Mpan = mpan,
                    Euid = euid,
                    PropertyId = propertyId,
                    CreatedAt = DateTime.UtcNow
                };
                db.HeatPumpDevices.Add(device);
            }
            else
            {
                device.DeviceId = deviceId;
                device.MeterSerialNumber = serialNumber;
                device.Mpan = mpan;
                device.Euid = euid;
                device.PropertyId = propertyId;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new { deviceId, mpan, serialNumber, euid, propertyId, message = "Device setup complete" });
        }).WithName("SetupHeatPump");

        // Get current telemetry
        group.MapGet("/telemetry/{deviceId}", async (string deviceId, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId);
            if (error is not null)
                return error;

            var telemetry = await client.GetSmartMeterTelemetryAsync(settings!.ApiKey, deviceId);
            return Results.Ok(telemetry);
        }).WithName("GetTelemetry");

        // Sync historical data
        group.MapPost("/sync/{deviceId}", async (string deviceId, DateTime? from, DateTime? to,
            OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId);
            if (error is not null)
                return error;

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var consumption = await client.GetConsumptionHistoryAsync(settings!.ApiKey, device!.Mpan!, device.MeterSerialNumber!, from.Value, to.Value);
            var results = consumption.RootElement.GetProperty("results");

            var existing = await db.ConsumptionReadings
                .Where(r => r.DeviceId == deviceId && r.ReadAt >= from && r.ReadAt <= to)
                .Select(r => r.ReadAt)
                .ToListAsync();

            var existingSet = new HashSet<DateTime>(existing);
            var readings = new List<ConsumptionReading>();

            foreach (var item in results.EnumerateArray())
            {
                var readAt = item.GetProperty("interval_start").GetDateTime();
                if (existingSet.Contains(readAt))
                    continue;

                var reading = new ConsumptionReading
                {
                    DeviceId = deviceId,
                    ReadAt = readAt,
                    Consumption = item.GetProperty("consumption").GetDecimal(),
                    CreatedAt = DateTime.UtcNow
                };

                readings.Add(reading);
            }

            db.ConsumptionReadings.AddRange(readings);
            device!.LastSyncAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { synced = readings.Count, from, to });
        }).WithName("SyncConsumption");

        // Get consumption data
        group.MapGet("/consumption/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var readings = await db.ConsumptionReadings
                .Where(r => r.DeviceId == deviceId && r.ReadAt >= from && r.ReadAt <= to)
                .OrderBy(r => r.ReadAt)
                .ToListAsync();

            return Results.Ok(readings);
        }).WithName("GetConsumption");

        // Get devices
        group.MapGet("/devices", async (CosyDbContext db) =>
        {
            var devices = await db.HeatPumpDevices.Where(d => d.IsActive).ToListAsync();
            return Results.Ok(devices);
        }).WithName("GetDevices");

        // Get account properties (needed for heat pump device query)
        group.MapGet("/properties/{accountNumber}", async (string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var viewerData = await client.GetViewerPropertiesAsync(settings!.ApiKey);
            var viewer = viewerData.RootElement.GetProperty("data").GetProperty("viewer");
            var accounts = viewer.GetProperty("accounts");
            var account = FindAccount(accounts, accountNumber);

            if (!account.HasValue)
                return Results.NotFound("Account not found in viewer response");

            var properties = account.Value.GetProperty("properties");
            return Results.Ok(properties);
        }).WithName("GetProperties");

        // Get heat pump device info
        group.MapGet("/heatpump-device/{accountNumber}/{propertyId}", async (string accountNumber, int propertyId, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var heatPump = await client.GetHeatPumpDeviceAsync(settings!.ApiKey, accountNumber, propertyId);
            return Results.Ok(heatPump);
        }).WithName("GetHeatPumpDevice");

        // Get heat pump status
        group.MapGet("/heatpump-status", async (string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var status = await client.GetHeatPumpStatusAsync(settings!.ApiKey);
            return Results.Ok(status);
        }).WithName("GetHeatPumpStatus");

        // Get viewer properties with heat pump device details
        group.MapGet("/heatpump-config", async (string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var config = await client.GetViewerPropertiesWithDevicesAsync(settings!.ApiKey);
            return Results.Ok(config);
        }).WithName("GetHeatPumpConfig");

        // Get heat pump controller status (uses basic heatPumpStatus query)
        group.MapGet("/heatpump-controller-status", async (string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var status = await client.GetHeatPumpStatusAsync(settings!.ApiKey);
            return Results.Ok(status);
        }).WithName("GetHeatPumpControllerStatus");

        // Get heat pump variants
        group.MapGet("/heatpump-variants", async (string accountNumber, string? make, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var variants = await client.GetHeatPumpVariantsAsync(settings!.ApiKey, make);
            return Results.Ok(variants);
        }).WithName("GetHeatPumpVariants");

        // Get complete heat pump data (COP, temperatures, performance — uses primary batched query)
        group.MapGet("/heatpump-complete/{accountNumber}/{euid}", async (string accountNumber, string euid, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.AccountNumber == accountNumber);
            if (device is null)
                return Results.NotFound("Device not found for this account");

            var data = await client.GetHeatPumpStatusAndConfigAsync(settings!.ApiKey, accountNumber, euid);
            return Results.Ok(data);
        }).WithName("GetHeatPumpCompleteData");

        group.MapPost("/graphql", async (GraphqlQueryRequest request, OctopusEnergyClient client, CosyDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return Results.BadRequest("Account number is required.");

            if (string.IsNullOrWhiteSpace(request.Query))
                return Results.BadRequest("Query is required.");

            var (settings, error) = await GetSettingsForAccountAsync(db, request.AccountNumber);
            if (error is not null)
                return error;

            var result = await client.ExecuteRawQueryAsync(settings!.ApiKey, request.Query, request.Variables);
            return Results.Ok(result);
        }).WithName("RunGraphqlQuery");

        group.MapGet("/controller-euids/{accountNumber}", async (string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var euids = await client.GetHeatPumpControllerEuidsAsync(settings!.ApiKey, accountNumber);
            return Results.Ok(euids);
        }).WithName("GetHeatPumpControllerEuids");

        group.MapGet("/summary/{deviceId}", async (string deviceId, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId);
            if (error is not null)
                return error;

            if (string.IsNullOrWhiteSpace(device!.Euid))
                return Results.Problem("EUID not found for device. Run setup first.");

            var data = await client.GetHeatPumpStatusAndConfigAsync(settings!.ApiKey, device.AccountNumber, device.Euid);
            var root = data.RootElement.GetProperty("data");

            var summary = MapHeatPumpSummary(root);
            return Results.Ok(summary);
        }).WithName("GetHeatPumpSummary");

        group.MapGet("/snapshots/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var snapshots = await db.HeatPumpSnapshots
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .OrderBy(s => s.SnapshotTakenAt)
                .ToListAsync();

            return Results.Ok(new
            {
                deviceId,
                from,
                to,
                count = snapshots.Count,
                snapshots
            });
        }).WithName("GetHeatPumpSnapshots");

        group.MapGet("/time-ranged/{accountNumber}/{euid}", async (string accountNumber, string euid, DateTime? from, DateTime? to, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var data = await client.GetHeatPumpTimeRangedPerformanceAsync(settings!.ApiKey, euid, from.Value, to.Value);
            var root = data.RootElement.GetProperty("data");
            
            return Results.Ok(new
            {
                accountNumber,
                euid,
                from,
                to,
                data = root
            });
        }).WithName("GetHeatPumpTimeRangedPerformance");

        group.MapGet("/time-series/{accountNumber}/{euid}", async (string accountNumber, string euid, DateTime? from, DateTime? to, string? grouping, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var span = to.Value - from.Value;
            var validationError = (grouping?.ToUpperInvariant()) switch
            {
                "LIVE"  when span > TimeSpan.FromHours(1)     => "Live grouping requires a time period of at most 1 hour (minute-by-minute data).",
                "DAY"   when span > TimeSpan.FromDays(2)      => "Day grouping requires a time period of at most 2 days (hour-by-hour data).",
                "WEEK"  when span > TimeSpan.FromDays(14)     => "Week grouping requires a time period of at most 14 days (day-by-day data).",
                "MONTH" when span > TimeSpan.FromDays(60)     => "Month grouping requires a time period of at most 60 days (day-by-day data).",
                "YEAR"  when span > TimeSpan.FromDays(13 * 31) => "Year grouping requires a time period of at most 13 months (day-by-day data).",
                _ => null
            };

            if (validationError is not null)
                return Results.BadRequest(new { error = validationError });

            var data = await client.GetHeatPumpTimeSeriesPerformanceAsync(settings!.ApiKey, euid, from.Value, to.Value, grouping);
            var root = data.RootElement.GetProperty("data");
            
            return Results.Ok(new
            {
                accountNumber,
                euid,
                from,
                to,
                grouping = grouping ?? "auto",
                data = root
            });
        }).WithName("GetHeatPumpTimeSeriesPerformance");
    }

    private static HeatPumpSummary MapHeatPumpSummary(JsonElement data)
    {
        return new HeatPumpSummary
        {
            ControllerStatus = MapControllerStatus(data),
            ControllerConfiguration = MapControllerConfiguration(data),
            LivePerformance = MapLivePerformance(data),
            LifetimePerformance = MapLifetimePerformance(data)
        };
    }

    private static HeatPumpControllerStatus? MapControllerStatus(JsonElement data)
    {
        if (!data.TryGetProperty("octoHeatPumpControllerStatus", out var status))
            return null;

        var sensors = status.TryGetProperty("sensors", out var sensorsEl)
            ? sensorsEl.EnumerateArray().Select(MapSensor).ToList()
            : new List<HeatPumpSensor>();

        var zones = status.TryGetProperty("zones", out var zonesEl)
            ? zonesEl.EnumerateArray().Select(MapZoneStatus).ToList()
            : new List<HeatPumpZoneStatus>();

        return new HeatPumpControllerStatus
        {
            Sensors = sensors,
            Zones = zones
        };
    }

    private static HeatPumpSensor MapSensor(JsonElement element)
    {
        return new HeatPumpSensor
        {
            Code = GetString(element, "code"),
            Connectivity = element.TryGetProperty("connectivity", out var connectivity)
                ? new HeatPumpConnectivity
                {
                    Online = GetBool(connectivity, "online"),
                    RetrievedAt = GetString(connectivity, "retrievedAt")
                }
                : null,
            Telemetry = element.TryGetProperty("telemetry", out var telemetry)
                ? new HeatPumpTelemetry
                {
                    TemperatureInCelsius = GetDecimal(telemetry, "temperatureInCelsius"),
                    HumidityPercentage = GetDecimal(telemetry, "humidityPercentage"),
                    RetrievedAt = GetString(telemetry, "retrievedAt")
                }
                : null
        };
    }

    private static HeatPumpZoneStatus MapZoneStatus(JsonElement element)
    {
        return new HeatPumpZoneStatus
        {
            Zone = GetString(element, "zone"),
            Telemetry = element.TryGetProperty("telemetry", out var telemetry)
                ? new HeatPumpZoneTelemetry
                {
                    SetpointInCelsius = GetDecimal(telemetry, "setpointInCelsius"),
                    Mode = GetString(telemetry, "mode"),
                    RelaySwitchedOn = GetBool(telemetry, "relaySwitchedOn"),
                    HeatDemand = GetBool(telemetry, "heatDemand"),
                    RetrievedAt = GetString(telemetry, "retrievedAt")
                }
                : null
        };
    }

    private static HeatPumpControllerConfiguration? MapControllerConfiguration(JsonElement data)
    {
        if (!data.TryGetProperty("octoHeatPumpControllerConfiguration", out var configuration))
            return null;

        var controller = configuration.TryGetProperty("controller", out var controllerEl)
            ? new HeatPumpController
            {
                State = GetStringList(controllerEl, "state"),
                HeatPumpTimezone = GetString(controllerEl, "heatPumpTimezone"),
                Connected = GetBool(controllerEl, "connected")
            }
            : null;

        var heatPump = configuration.TryGetProperty("heatPump", out var heatPumpEl)
            ? MapHeatPumpDetails(heatPumpEl)
            : null;

        var zones = configuration.TryGetProperty("zones", out var zonesEl)
            ? zonesEl.EnumerateArray().Select(MapZoneConfiguration).ToList()
            : new List<HeatPumpZoneConfiguration>();

        return new HeatPumpControllerConfiguration
        {
            Controller = controller,
            HeatPump = heatPump,
            Zones = zones
        };
    }

    private static HeatPumpDetails MapHeatPumpDetails(JsonElement element)
    {
        return new HeatPumpDetails
        {
            SerialNumber = GetString(element, "serialNumber"),
            Model = GetString(element, "model"),
            HardwareVersion = GetString(element, "hardwareVersion"),
            MaxWaterSetpoint = GetInt(element, "maxWaterSetpoint"),
            MinWaterSetpoint = GetInt(element, "minWaterSetpoint"),
            HeatingFlowTemperature = element.TryGetProperty("heatingFlowTemperature", out var flow)
                ? new HeatPumpHeatingFlowTemperature
                {
                    CurrentTemperature = MapValueAndUnit(flow, "currentTemperature"),
                    AllowableRange = MapAllowableRange(flow, "allowableRange")
                }
                : null,
            WeatherCompensation = element.TryGetProperty("weatherCompensation", out var weather)
                ? new HeatPumpWeatherCompensation
                {
                    Enabled = GetBool(weather, "enabled"),
                    CurrentRange = MapAllowableRange(weather, "currentRange")
                }
                : null
        };
    }

    private static HeatPumpZoneConfiguration MapZoneConfiguration(JsonElement element)
    {
        return new HeatPumpZoneConfiguration
        {
            Configuration = element.TryGetProperty("configuration", out var config)
                ? MapZoneConfig(config)
                : null
        };
    }

    private static HeatPumpZoneConfig MapZoneConfig(JsonElement config)
    {
        var sensors = config.TryGetProperty("sensors", out var sensorsEl)
            ? sensorsEl.EnumerateArray().Select(MapSensorConfiguration).ToList()
            : new List<HeatPumpSensorConfiguration>();

        return new HeatPumpZoneConfig
        {
            Code = GetString(config, "code"),
            ZoneType = GetString(config, "zoneType"),
            Enabled = GetBool(config, "enabled"),
            DisplayName = GetString(config, "displayName"),
            PrimarySensor = GetString(config, "primarySensor"),
            CurrentOperation = config.TryGetProperty("currentOperation", out var operation)
                ? new HeatPumpCurrentOperation
                {
                    Mode = GetString(operation, "mode"),
                    SetpointInCelsius = GetDecimal(operation, "setpointInCelsius"),
                    Action = GetString(operation, "action"),
                    End = GetString(operation, "end")
                }
                : null,
            CallForHeat = GetBool(config, "callForHeat"),
            HeatDemand = GetBool(config, "heatDemand"),
            Emergency = GetBool(config, "emergency"),
            Sensors = sensors
        };
    }

    private static HeatPumpSensorConfiguration MapSensorConfiguration(JsonElement sensor)
    {
        return new HeatPumpSensorConfiguration
        {
            Code = GetString(sensor, "code"),
            DisplayName = GetString(sensor, "displayName"),
            Type = GetString(sensor, "type"),
            Enabled = GetBool(sensor, "enabled"),
            FirmwareVersion = GetString(sensor, "firmwareVersion"),
            BoostEnabled = GetBool(sensor, "boostEnabled")
        };
    }

    private static HeatPumpLivePerformance? MapLivePerformance(JsonElement data)
    {
        if (!data.TryGetProperty("octoHeatPumpLivePerformance", out var live))
            return null;

        return new HeatPumpLivePerformance
        {
            CoefficientOfPerformance = GetString(live, "coefficientOfPerformance"),
            OutdoorTemperature = MapValueAndUnit(live, "outdoorTemperature"),
            HeatOutput = MapValueAndUnit(live, "heatOutput"),
            PowerInput = MapValueAndUnit(live, "powerInput"),
            ReadAt = GetString(live, "readAt")
        };
    }

    private static HeatPumpLifetimePerformance? MapLifetimePerformance(JsonElement data)
    {
        if (!data.TryGetProperty("octoHeatPumpLifetimePerformance", out var lifetime))
            return null;

        return new HeatPumpLifetimePerformance
        {
            SeasonalCoefficientOfPerformance = GetString(lifetime, "seasonalCoefficientOfPerformance"),
            HeatOutput = MapValueAndUnit(lifetime, "heatOutput"),
            EnergyInput = MapValueAndUnit(lifetime, "energyInput"),
            ReadAt = GetString(lifetime, "readAt")
        };
    }

    private static HeatPumpValueAndUnit? MapValueAndUnit(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueAndUnit))
            return null;

        return new HeatPumpValueAndUnit
        {
            Value = GetString(valueAndUnit, "value"),
            Unit = GetString(valueAndUnit, "unit")
        };
    }

    private static HeatPumpAllowableRange? MapAllowableRange(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var range))
            return null;

        return new HeatPumpAllowableRange
        {
            Minimum = MapValueAndUnit(range, "minimum"),
            Maximum = MapValueAndUnit(range, "maximum")
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null
            : null;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsed)
            ? parsed
            : decimal.TryParse(value.ToString(), out var textParsed) ? textParsed : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : int.TryParse(value.ToString(), out var textParsed) ? textParsed : null;
    }

    private static List<string> GetStringList(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<string>();

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    public sealed record GraphqlQueryRequest(string AccountNumber, string Query, JsonElement? Variables);
}

