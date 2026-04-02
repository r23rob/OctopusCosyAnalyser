using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;
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

            // Third fallback: try octoHeatPumpControllersAtLocation if we have a propertyId
            if (string.IsNullOrWhiteSpace(euid) && propertyId.HasValue)
            {
                try
                {
                    var controllersData = await client.GetHeatPumpControllersAtLocationAsync(settings.ApiKey, accountNumber, propertyId.Value);
                    if (controllersData.RootElement.TryGetProperty("data", out var locData)
                        && locData.TryGetProperty("octoHeatPumpControllersAtLocation", out var controllers)
                        && controllers.ValueKind == JsonValueKind.Array
                        && controllers.GetArrayLength() > 0)
                    {
                        euid = controllers[0].GetString();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch controllers at location: {ex.Message}");
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
        group.MapGet("/consumption/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, int? skip, int? take, CosyDbContext db) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;
            var safeSkip = Math.Max(skip ?? 0, 0);
            var actualTake = Math.Clamp(take ?? 10000, 1, 50000);

            var readings = await db.ConsumptionReadings
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.ReadAt >= from && r.ReadAt <= to)
                .OrderBy(r => r.ReadAt)
                .Skip(safeSkip)
                .Take(actualTake)
                .ToListAsync();

            return Results.Ok(readings);
        }).WithName("GetConsumption");

        // Get devices
        group.MapGet("/devices", async (CosyDbContext db) =>
        {
            var devices = await db.HeatPumpDevices.AsNoTracking().Where(d => d.IsActive).ToListAsync();
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

        if (app.Environment.IsDevelopment())
        {
            group.MapGet("/introspect/{typeName}", async (string typeName, string accountNumber, OctopusEnergyClient client, CosyDbContext db) =>
            {
                var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
                if (error is not null)
                    return error;

                var introspectionQuery = $"{{ __type(name: \"{typeName}\") {{ name kind fields {{ name args {{ name type {{ name kind ofType {{ name kind ofType {{ name kind }} }} }} defaultValue }} type {{ name kind ofType {{ name kind ofType {{ name kind }} }} }} }} }} }}";
                var result = await client.ExecuteRawQueryAsync(settings!.ApiKey, introspectionQuery);
                return Results.Ok(result);
            }).WithName("IntrospectType");

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
        }

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

        group.MapGet("/snapshots/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, int? skip, int? take, CosyDbContext db) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;
            var safeSkip = Math.Max(skip ?? 0, 0);
            var actualTake = Math.Clamp(take ?? 10000, 1, 50000);

            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .OrderBy(s => s.SnapshotTakenAt)
                .Skip(safeSkip)
                .Take(actualTake)
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

        group.MapGet("/snapshots/{deviceId}/latest", async (string deviceId, CosyDbContext db) =>
        {
            var latest = await db.HeatPumpSnapshots
                .Where(s => s.DeviceId == deviceId)
                .OrderByDescending(s => s.SnapshotTakenAt)
                .Select(s => new { s.SnapshotTakenAt })
                .FirstOrDefaultAsync();

            if (latest is null)
                return Results.Ok(new LatestSnapshotDto { HasData = false });

            var minutesAgo = (DateTime.UtcNow - latest.SnapshotTakenAt).TotalMinutes;
            return Results.Ok(new LatestSnapshotDto
            {
                HasData = true,
                SnapshotTakenAt = latest.SnapshotTakenAt,
                MinutesAgo = minutesAgo
            });
        }).WithName("GetLatestSnapshot");

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

        // ── Time Series – Persisted (DB) ──────────────────────────────

        group.MapGet("/timeseries/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            if (from > to)
                return Results.BadRequest(new { error = "'from' must be before 'to'." });

            // Normalise to UTC for consistent querying
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);

            var records = await db.HeatPumpTimeSeriesRecords
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.StartAt >= fromUtc && r.StartAt <= toUtc)
                .OrderBy(r => r.StartAt)
                .Take(50000)
                .ToListAsync();

            return Results.Ok(new
            {
                deviceId,
                from = fromUtc,
                to = toUtc,
                count = records.Count,
                records = records.Select(r => new
                {
                    r.StartAt,
                    r.EndAt,
                    r.EnergyInputKwh,
                    r.EnergyOutputKwh,
                    r.OutdoorTemperatureCelsius
                })
            });
        }).WithName("GetStoredTimeSeries");

        group.MapPost("/sync-timeseries/{deviceId}", async (string deviceId, DateTime? from, DateTime? to,
            OctopusEnergyClient client, CosyDbContext db, ILoggerFactory loggerFactory) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId);
            if (error is not null)
                return error;

            if (string.IsNullOrWhiteSpace(device!.Euid))
                return Results.BadRequest(new { error = "Device has no EUID. Run setup first." });

            from ??= DateTime.UtcNow.AddMonths(-12);
            to ??= DateTime.UtcNow;

            if (from > to)
                return Results.BadRequest(new { error = "'from' must be before 'to'." });

            if ((to.Value - from.Value).TotalDays > 400)
                return Results.BadRequest(new { error = "Maximum sync range is 400 days." });

            var synced = 0;
            var skipped = 0;
            var chunkStart = from.Value;
            var chunkSize = TimeSpan.FromDays(60); // MONTH grouping max span
            var logger = loggerFactory.CreateLogger("TimeSeriesSync");

            // Load all existing timestamps for this device in the date range to avoid duplicates
            var existingTimestamps = await db.HeatPumpTimeSeriesRecords
                .Where(r => r.DeviceId == deviceId && r.StartAt >= from.Value && r.StartAt <= to.Value)
                .Select(r => r.StartAt)
                .ToListAsync();
            var existingSet = new HashSet<DateTime>(existingTimestamps);

            while (chunkStart < to.Value)
            {
                var chunkEnd = chunkStart + chunkSize;
                if (chunkEnd > to.Value)
                    chunkEnd = to.Value;

                try
                {
                    var data = await client.GetHeatPumpTimeSeriesPerformanceAsync(
                        settings!.ApiKey, device.Euid, chunkStart, chunkEnd, "MONTH");
                    var root = data.RootElement.GetProperty("data");

                    var chunkSynced = 0;
                    if (root.TryGetProperty("octoHeatPumpTimeSeriesPerformance", out var series)
                        && series.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in series.EnumerateArray())
                        {
                            if (!item.TryGetProperty("startAt", out var startAtEl)
                                || !DateTimeOffset.TryParse(startAtEl.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var startAtDto))
                                continue;

                            var startAt = startAtDto.UtcDateTime;

                            // Skip records that already exist in the database
                            if (existingSet.Contains(startAt))
                            {
                                skipped++;
                                continue;
                            }

                            var endAtUtc = startAt.AddHours(1); // default for hourly buckets
                            if (item.TryGetProperty("endAt", out var endAtEl)
                                && DateTimeOffset.TryParse(endAtEl.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var endAtDto))
                                endAtUtc = endAtDto.UtcDateTime;

                            var record = new HeatPumpTimeSeriesRecord
                            {
                                DeviceId = deviceId,
                                StartAt = startAt,
                                EndAt = endAtUtc,
                                CreatedAt = DateTime.UtcNow
                            };

                            if (item.TryGetProperty("energyInput", out var ei) && ei.TryGetProperty("value", out var eiVal)
                                && decimal.TryParse(eiVal.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var eiDec))
                                record.EnergyInputKwh = eiDec;

                            if (item.TryGetProperty("energyOutput", out var eo) && eo.TryGetProperty("value", out var eoVal)
                                && decimal.TryParse(eoVal.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var eoDec))
                                record.EnergyOutputKwh = eoDec;

                            if (item.TryGetProperty("outdoorTemperature", out var ot) && ot.TryGetProperty("value", out var otVal)
                                && decimal.TryParse(otVal.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var otDec))
                                record.OutdoorTemperatureCelsius = otDec;

                            db.HeatPumpTimeSeriesRecords.Add(record);
                            existingSet.Add(startAt); // prevent duplicates within this sync run
                            chunkSynced++;
                        }
                    }

                    // Save per chunk to bound memory and make partial progress durable
                    if (chunkSynced > 0)
                    {
                        await db.SaveChangesAsync();
                        db.ChangeTracker.Clear();
                        synced += chunkSynced;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to sync time-series chunk {From} to {To} for device {DeviceId}",
                        chunkStart, chunkEnd, deviceId);
                }

                chunkStart = chunkEnd;
            }

            return Results.Ok(new { synced, skipped, from, to });
        }).WithName("SyncTimeSeries");

        // ── Controllers at Location (Multi-HP) ────────────────────────

        group.MapGet("/controllers-at-location/{accountNumber}/{propertyId:int}", async (string accountNumber, int propertyId, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var data = await client.GetHeatPumpControllersAtLocationAsync(settings!.ApiKey, accountNumber, propertyId);
            return Results.Ok(data);
        }).WithName("GetControllersAtLocation");

        // ── Applicable Rates (Tariff) ─────────────────────────────────

        group.MapGet("/rates/{accountNumber}", async (string accountNumber, DateTime? from, DateTime? to, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            var device = await db.HeatPumpDevices
                .FirstOrDefaultAsync(d => d.AccountNumber == accountNumber && d.IsActive && d.Mpan != null);
            if (device is null)
                return Results.BadRequest(new { error = "No active device with MPAN found for this account. Run setup first." });

            from ??= DateTime.UtcNow.AddDays(-1);
            to ??= DateTime.UtcNow;

            var data = await client.GetApplicableRatesAsync(settings!.ApiKey, accountNumber, device.Mpan!, from.Value, to.Value);
            var root = data.RootElement.GetProperty("data");

            // Surface any GraphQL errors so the UI can display them
            JsonElement? errors = data.RootElement.TryGetProperty("errors", out var errEl) ? errEl : null;

            return Results.Ok(new
            {
                accountNumber,
                mpan = device.Mpan,
                from,
                to,
                data = root,
                errors
            });
        }).WithName("GetApplicableRates");

        // ── Cost of Usage ─────────────────────────────────────────────

        group.MapGet("/cost/{accountNumber}", async (string accountNumber, DateTime? from, DateTime? to, OctopusEnergyClient client, CosyDbContext db) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber);
            if (error is not null)
                return error;

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var data = await client.GetCostOfUsageAsync(settings!.ApiKey, accountNumber, from.Value, to.Value);
            var root = data.RootElement.GetProperty("data");

            return Results.Ok(new
            {
                accountNumber,
                from,
                to,
                data = root
            });
        }).WithName("GetCostOfUsage");

        // ── Daily Aggregates ─────────────────────────────────────────

        group.MapGet("/daily-aggregates/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db) =>
        {
            from ??= DateTime.UtcNow.AddDays(-30);
            to ??= DateTime.UtcNow;

            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .OrderBy(s => s.SnapshotTakenAt)
                .ToListAsync();

            var aggregates = ComputeDailyAggregates(snapshots);

            return Results.Ok(new
            {
                deviceId,
                from,
                to,
                totalSnapshots = snapshots.Count,
                days = aggregates.Count,
                aggregates
            });
        }).WithName("GetDailyAggregates");

        // ── AI Analysis ──────────────────────────────────────────────

        group.MapPost("/ai-analysis/{deviceId}", async (string deviceId, AiAnalysisRequestDto request,
            AiAnalysisService aiService, OctopusEnergyClient octopusClient, CosyDbContext db,
            ILogger<AiAnalysisService> logger) =>
        {
            if (request.From >= request.To)
                return Results.BadRequest(new { error = "From date must be before To date." });

            if ((request.To - request.From).TotalDays > 365)
                return Results.BadRequest(new { error = "Date range must not exceed 365 days." });

            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device is null)
                return Results.NotFound("Device not found");

            var from = request.From;
            var to = request.To;

            // Load snapshots (for weather comp, flow temp, room temp, zones, COP, etc.)
            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .OrderBy(s => s.SnapshotTakenAt)
                .ToListAsync();

            // Load time series history (synced hourly energy data)
            var timeSeriesRecords = await db.HeatPumpTimeSeriesRecords
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.StartAt >= from && r.StartAt <= to)
                .OrderBy(r => r.StartAt)
                .ToListAsync();

            if (snapshots.Count == 0 && timeSeriesRecords.Count == 0)
                return Results.Ok(new AiAnalysisResponseDto
                {
                    Analysis = "No data found for the selected period. Try selecting a wider date range, or sync history data first.",
                    From = from,
                    To = to,
                    DaysAnalysed = 0,
                    TotalSnapshots = 0
                });

            // Compute daily aggregates from snapshots (existing logic)
            var aggregates = snapshots.Count > 0
                ? ComputeDailyAggregates(snapshots)
                : new List<DailyAggregateDto>();

            // Enrich with time series data + weather comp from nearest snapshots
            if (timeSeriesRecords.Count > 0)
            {
                EnrichAggregatesWithTimeSeries(aggregates, timeSeriesRecords, snapshots);
            }

            // Merge cost data from Octopus API
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber);

            var costDataStatus = "No account settings found";
            if (settings is not null)
            {
                try
                {
                    logger.LogInformation("Fetching cost data for account {Account} from {From} to {To}",
                        device.AccountNumber, from, to);

                    var costData = await octopusClient.GetCostOfUsageAsync(settings.ApiKey, device.AccountNumber, from, to);
                    var costRoot = costData.RootElement.GetProperty("data");

                    // Check for GraphQL errors
                    if (costData.RootElement.TryGetProperty("errors", out var errorsEl)
                        && errorsEl.ValueKind == JsonValueKind.Array
                        && errorsEl.GetArrayLength() > 0)
                    {
                        var errorMsgs = string.Join("; ", errorsEl.EnumerateArray()
                            .Select(e => e.TryGetProperty("message", out var m) ? m.GetString() : e.ToString()));
                        costDataStatus = $"GraphQL errors: {errorMsgs}";
                        logger.LogWarning("Cost data query returned errors for device {DeviceId}: {Errors}", deviceId, errorMsgs);
                    }
                    else if (costRoot.TryGetProperty("costOfUsage", out var costEl)
                        && costEl.ValueKind != JsonValueKind.Null
                        && costEl.TryGetProperty("edges", out var edges)
                        && edges.ValueKind == JsonValueKind.Array)
                    {
                        var costByDate = new Dictionary<DateOnly, (double cost, double usage, double unitRate)>();
                        foreach (var edge in edges.EnumerateArray())
                        {
                            if (!edge.TryGetProperty("node", out var node)) continue;
                            var startAt = node.TryGetProperty("startAt", out var sa) ? sa.GetString() : null;
                            if (startAt == null || !DateTime.TryParse(startAt, out var dt)) continue;

                            var date = DateOnly.FromDateTime(dt);
                            var cost = GetJsonDouble(node, "costInclTax");
                            var usage = GetJsonDouble(node, "consumptionKwh");
                            var unitRate = GetJsonDouble(node, "unitRateInclTax");

                            if (costByDate.TryGetValue(date, out var existing))
                            {
                                var newCost = existing.cost + cost;
                                var newUsage = existing.usage + usage;
                                var newUnitRate = newUsage > 0 ? newCost / newUsage : existing.unitRate;
                                costByDate[date] = (newCost, newUsage, newUnitRate);
                            }
                            else
                            {
                                costByDate[date] = (cost, usage, unitRate);
                            }
                        }

                        var mergedCount = 0;
                        foreach (var agg in aggregates)
                        {
                            if (costByDate.TryGetValue(agg.Date, out var costInfo))
                            {
                                agg.DailyCostPence = costInfo.cost;
                                agg.DailyUsageKwh = costInfo.usage;
                                agg.AvgUnitRatePence = costInfo.unitRate;
                                agg.CostPerKwhHeatPence = agg.TotalHeatOutputKwh > 0
                                    ? costInfo.cost / agg.TotalHeatOutputKwh
                                    : null;
                                mergedCount++;
                            }
                        }

                        costDataStatus = $"OK: {costByDate.Count} days of cost data, merged into {mergedCount} aggregates";
                        logger.LogInformation("Merged cost data for device {DeviceId}: {Status}", deviceId, costDataStatus);
                    }
                    else
                    {
                        costDataStatus = "costOfUsage returned null or empty";
                        logger.LogWarning("Cost data query returned null/empty for device {DeviceId}", deviceId);
                    }
                }
                catch (Exception ex)
                {
                    costDataStatus = $"Error: {ex.Message}";
                    logger.LogWarning(ex, "Failed to merge Octopus cost data for device {DeviceId}", deviceId);
                }
            }

            var analysis = await aiService.AnalyseAsync(aggregates, request.Question, settings?.AnthropicApiKey);

            return Results.Ok(new AiAnalysisResponseDto
            {
                Analysis = analysis,
                From = from,
                To = to,
                DaysAnalysed = aggregates.Count,
                TotalSnapshots = snapshots.Count,
                TotalTimeSeriesRecords = timeSeriesRecords.Count,
                CostDataStatus = costDataStatus
            });
        }).WithName("GetAiAnalysis");

        // AI Dashboard Summary (auto-cached, lightweight)
        group.MapGet("/ai-summary/{deviceId}", async (string deviceId, HeatPumpAiService aiService, CosyDbContext db) =>
        {
            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device is null)
                return Results.NotFound("Device not found");

            var summary = await aiService.GenerateSummaryAsync(deviceId);
            return Results.Ok(summary);
        }).WithName("GetAiSummary");

        group.MapGet("/ai-summary/{deviceId}/refresh", async (string deviceId, HeatPumpAiService aiService, CosyDbContext db) =>
        {
            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device is null)
                return Results.NotFound("Device not found");

            var summary = await aiService.GenerateSummaryAsync(deviceId, forceRefresh: true);
            return Results.Ok(summary);
        }).WithName("RefreshAiSummary");
    }

    internal static List<DailyAggregateDto> ComputeDailyAggregates(List<HeatPumpSnapshot> snapshots)
    {
        return snapshots
            .GroupBy(s => DateOnly.FromDateTime(s.SnapshotTakenAt))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var day = g.ToList();
                var heatingSnapshots = day.Where(s => s.HeatingZoneHeatDemand == true).ToList();
                var hotWaterSnapshots = day.Where(s => s.HotWaterZoneHeatDemand == true).ToList();
                var spaceHeatingOnly = day.Where(s => s.HeatingZoneHeatDemand == true && s.HotWaterZoneHeatDemand != true).ToList();

                // Controller state transitions
                var stateTransitions = 0;
                string? prevState = null;
                foreach (var s in day)
                {
                    if (prevState != null && s.ControllerState != prevState)
                        stateTransitions++;
                    prevState = s.ControllerState;
                }

                // Hot water run count (distinct periods of consecutive HW demand)
                var hwRunCount = 0;
                var hwTotalSnapshots = 0;
                var prevHwDemand = false;
                foreach (var s in day)
                {
                    var hwDemand = s.HotWaterZoneHeatDemand == true;
                    if (hwDemand && !prevHwDemand)
                        hwRunCount++;
                    if (hwDemand)
                        hwTotalSnapshots++;
                    prevHwDemand = hwDemand;
                }

                // Weather compensation mode (most frequent non-null value)
                var wcEnabledMode = day.Where(s => s.WeatherCompensationEnabled.HasValue)
                    .GroupBy(s => s.WeatherCompensationEnabled!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                var wcMinMode = day.Where(s => s.WeatherCompensationMinCelsius.HasValue)
                    .GroupBy(s => s.WeatherCompensationMinCelsius!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                var wcMaxMode = day.Where(s => s.WeatherCompensationMaxCelsius.HasValue)
                    .GroupBy(s => s.WeatherCompensationMaxCelsius!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                return new DailyAggregateDto
                {
                    Date = g.Key,
                    SnapshotCount = day.Count,

                    AvgCopHeating = AvgDecimal(heatingSnapshots, s => s.CoefficientOfPerformance),
                    AvgCopHotWater = AvgDecimal(hotWaterSnapshots, s => s.CoefficientOfPerformance),
                    AvgCopSpaceHeatingOnly = AvgDecimal(spaceHeatingOnly, s => s.CoefficientOfPerformance),

                    TotalElectricityKwh = day.Where(s => s.PowerInputKilowatt.HasValue)
                        .Sum(s => (double)s.PowerInputKilowatt!.Value * 0.25),
                    TotalHeatOutputKwh = day.Where(s => s.HeatOutputKilowatt.HasValue)
                        .Sum(s => (double)s.HeatOutputKilowatt!.Value * 0.25),

                    AvgOutdoorTemp = AvgDecimal(day, s => s.OutdoorTemperatureCelsius),
                    MinOutdoorTemp = day.Any(s => s.OutdoorTemperatureCelsius.HasValue)
                        ? day.Where(s => s.OutdoorTemperatureCelsius.HasValue)
                            .Select(s => (double)s.OutdoorTemperatureCelsius!.Value).Min()
                        : null,
                    MaxOutdoorTemp = day.Any(s => s.OutdoorTemperatureCelsius.HasValue)
                        ? day.Where(s => s.OutdoorTemperatureCelsius.HasValue)
                            .Select(s => (double)s.OutdoorTemperatureCelsius!.Value).Max()
                        : null,
                    // NOTE: This is the configured fixed flow temp SETPOINT, not a measured flow temperature
                    AvgFlowTemp = AvgDecimal(heatingSnapshots, s => s.HeatingFlowTemperatureCelsius),
                    AvgRoomTemp = AvgDecimal(day, s => s.RoomTemperatureCelsius),
                    AvgSetpoint = AvgDecimal(day, s => s.HeatingZoneSetpointCelsius),

                    HeatingDutyCyclePercent = day.Count > 0
                        ? day.Count(s => s.HeatingZoneHeatDemand == true) * 100.0 / day.Count
                        : 0,
                    HotWaterDutyCyclePercent = day.Count > 0
                        ? day.Count(s => s.HotWaterZoneHeatDemand == true) * 100.0 / day.Count
                        : 0,

                    WeatherCompEnabled = wcEnabledMode,
                    WeatherCompMin = wcMinMode.HasValue ? (double)wcMinMode.Value : null,
                    WeatherCompMax = wcMaxMode.HasValue ? (double)wcMaxMode.Value : null,

                    ControllerStateTransitions = stateTransitions,

                    HotWaterRunCount = hwRunCount,
                    HotWaterTotalMinutes = hwTotalSnapshots * 15,
                    AvgHotWaterSetpoint = AvgDecimal(hotWaterSnapshots, s => s.HotWaterZoneSetpointCelsius),
                };
            })
            .ToList();
    }

    private static double? AvgDecimal(List<HeatPumpSnapshot> snapshots, Func<HeatPumpSnapshot, decimal?> selector)
    {
        var values = snapshots.Where(s => selector(s).HasValue).Select(s => (double)selector(s)!.Value).ToList();
        return values.Count > 0 ? values.Average() : null;
    }

    /// <summary>
    /// Enriches daily aggregates with time series history data.
    /// For each time series record, finds the nearest snapshot within ±30 minutes
    /// to correlate weather compensation settings with energy performance.
    /// Creates new daily aggregates for dates that have time series data but no snapshots.
    /// </summary>
    internal static void EnrichAggregatesWithTimeSeries(
        List<DailyAggregateDto> aggregates,
        List<HeatPumpTimeSeriesRecord> timeSeriesRecords,
        List<HeatPumpSnapshot> snapshots)
    {
        // Index snapshots by time for efficient nearest-neighbour lookup
        var snapshotsByTime = snapshots
            .OrderBy(s => s.SnapshotTakenAt)
            .ToList();

        // Group time series records by date
        var tsByDate = timeSeriesRecords
            .GroupBy(r => DateOnly.FromDateTime(r.StartAt))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Index existing aggregates by date for merging
        var aggByDate = aggregates.ToDictionary(a => a.Date);

        foreach (var (date, records) in tsByDate)
        {
            // Sum energy from time series for the day
            var tsEnergyIn = records
                .Where(r => r.EnergyInputKwh.HasValue)
                .Sum(r => (double)r.EnergyInputKwh!.Value);
            var tsEnergyOut = records
                .Where(r => r.EnergyOutputKwh.HasValue)
                .Sum(r => (double)r.EnergyOutputKwh!.Value);
            var tsOutdoorTemps = records
                .Where(r => r.OutdoorTemperatureCelsius.HasValue)
                .Select(r => (double)r.OutdoorTemperatureCelsius!.Value)
                .ToList();

            // Find weather comp settings from nearest snapshots (within 30 min window)
            var wcValues = new List<(bool? enabled, decimal? min, decimal? max, decimal? flowTemp)>();
            foreach (var rec in records)
            {
                var nearest = FindNearestSnapshot(snapshotsByTime, rec.StartAt, TimeSpan.FromMinutes(30));
                if (nearest is not null)
                {
                    wcValues.Add((
                        nearest.WeatherCompensationEnabled,
                        nearest.WeatherCompensationMinCelsius,
                        nearest.WeatherCompensationMaxCelsius,
                        nearest.HeatingFlowTemperatureCelsius
                    ));
                }
            }

            // Compute weather comp mode values for the day from matched snapshots
            var wcEnabledMode = wcValues
                .Where(v => v.enabled.HasValue)
                .GroupBy(v => v.enabled!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            var wcMinMode = wcValues
                .Where(v => v.min.HasValue)
                .GroupBy(v => v.min!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            var wcMaxMode = wcValues
                .Where(v => v.max.HasValue)
                .GroupBy(v => v.max!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            var avgFlowTemp = wcValues
                .Where(v => v.flowTemp.HasValue)
                .Select(v => (double)v.flowTemp!.Value)
                .ToList();

            if (aggByDate.TryGetValue(date, out var existing))
            {
                // Merge: prefer time series energy totals (more complete hourly data)
                if (tsEnergyIn > 0)
                    existing.TotalElectricityKwh = tsEnergyIn;
                if (tsEnergyOut > 0)
                    existing.TotalHeatOutputKwh = tsEnergyOut;

                // Supplement outdoor temp from time series if snapshot data is missing
                if (!existing.AvgOutdoorTemp.HasValue && tsOutdoorTemps.Count > 0)
                    existing.AvgOutdoorTemp = tsOutdoorTemps.Average();
                if (!existing.MinOutdoorTemp.HasValue && tsOutdoorTemps.Count > 0)
                    existing.MinOutdoorTemp = tsOutdoorTemps.Min();
                if (!existing.MaxOutdoorTemp.HasValue && tsOutdoorTemps.Count > 0)
                    existing.MaxOutdoorTemp = tsOutdoorTemps.Max();

                // Supplement weather comp from time-series-correlated snapshots if not already set
                if (!existing.WeatherCompEnabled.HasValue && wcEnabledMode.HasValue)
                    existing.WeatherCompEnabled = wcEnabledMode.Value;
                if (!existing.WeatherCompMin.HasValue && wcMinMode.HasValue)
                    existing.WeatherCompMin = (double)wcMinMode.Value;
                if (!existing.WeatherCompMax.HasValue && wcMaxMode.HasValue)
                    existing.WeatherCompMax = (double)wcMaxMode.Value;
                if (!existing.AvgFlowTemp.HasValue && avgFlowTemp.Count > 0)
                    existing.AvgFlowTemp = avgFlowTemp.Average();

                // Compute COP from time series energy data
                if (tsEnergyIn > 0 && tsEnergyOut > 0)
                    existing.AvgCopHeating ??= tsEnergyOut / tsEnergyIn;
            }
            else
            {
                // Create new aggregate from time series data for dates without snapshots
                var newAgg = new DailyAggregateDto
                {
                    Date = date,
                    SnapshotCount = 0,
                    TotalElectricityKwh = tsEnergyIn,
                    TotalHeatOutputKwh = tsEnergyOut,
                    AvgOutdoorTemp = tsOutdoorTemps.Count > 0 ? tsOutdoorTemps.Average() : null,
                    MinOutdoorTemp = tsOutdoorTemps.Count > 0 ? tsOutdoorTemps.Min() : null,
                    MaxOutdoorTemp = tsOutdoorTemps.Count > 0 ? tsOutdoorTemps.Max() : null,
                    AvgCopHeating = tsEnergyIn > 0 ? tsEnergyOut / tsEnergyIn : null,
                    WeatherCompEnabled = wcEnabledMode,
                    WeatherCompMin = wcMinMode.HasValue ? (double)wcMinMode.Value : null,
                    WeatherCompMax = wcMaxMode.HasValue ? (double)wcMaxMode.Value : null,
                    AvgFlowTemp = avgFlowTemp.Count > 0 ? avgFlowTemp.Average() : null,
                };

                aggregates.Add(newAgg);
            }
        }

        // Re-sort by date after adding new entries
        aggregates.Sort((a, b) => a.Date.CompareTo(b.Date));
    }

    /// <summary>
    /// Finds the nearest snapshot to a given timestamp within a maximum time window.
    /// Uses binary search for efficiency on sorted snapshot list.
    /// </summary>
    private static HeatPumpSnapshot? FindNearestSnapshot(List<HeatPumpSnapshot> sortedSnapshots, DateTime target, TimeSpan maxDistance)
    {
        if (sortedSnapshots.Count == 0) return null;

        // Binary search for the insertion point
        var idx = sortedSnapshots.BinarySearch(null!, Comparer<HeatPumpSnapshot>.Create(
            (a, _) => a!.SnapshotTakenAt.CompareTo(target)));

        if (idx < 0) idx = ~idx; // bitwise complement gives the insertion point

        HeatPumpSnapshot? best = null;
        var bestDistance = TimeSpan.MaxValue;

        // Check the element at idx and idx-1 (the two nearest candidates)
        for (var i = Math.Max(0, idx - 1); i <= Math.Min(sortedSnapshots.Count - 1, idx); i++)
        {
            var distance = (sortedSnapshots[i].SnapshotTakenAt - target).Duration();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = sortedSnapshots[i];
            }
        }

        return bestDistance <= maxDistance ? best : null;
    }

    private static double GetJsonDouble(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var val)) return 0;
        if (val.ValueKind == JsonValueKind.Number && val.TryGetDouble(out var d)) return d;
        if (val.ValueKind == JsonValueKind.String
            && double.TryParse(val.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return 0;
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

