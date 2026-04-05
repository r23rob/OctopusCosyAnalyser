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
            CosyDbContext db, string deviceId, CancellationToken ct)
        {
            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
            if (device is null)
                return (null, null, Results.NotFound("Device not found"));

            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber, ct);

            if (settings is null)
                return (device, null, Results.Problem("Account settings not found. Save API key in /settings."));

            return (device, settings, null);
        }

        static async Task<(OctopusAccountSettings? Settings, IResult? Error)> GetSettingsForAccountAsync(
            CosyDbContext db, string accountNumber, CancellationToken ct)
        {
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber, ct);

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
        group.MapPost("/setup", async (string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, settingsError) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (settingsError is not null)
                return settingsError;

            // Get account data with electricity agreements
            var accountData = await client.GetAccountAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber);
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
                var viewerData = await client.GetViewerPropertiesAsync(settings.Email!, settings.OctopusPassword!);
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
                    var controllerEuids = await client.GetHeatPumpControllerEuidsAsync(settings.Email!, settings.OctopusPassword!, accountNumber);
                    if (controllerEuids.RootElement.TryGetProperty("data", out var controllerData)
                        && controllerData.TryGetProperty("heatPumpControllerEuids", out var euids)
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

            // Third fallback: try heatPumpControllersAtLocation if we have a propertyId
            if (string.IsNullOrWhiteSpace(euid) && propertyId.HasValue)
            {
                try
                {
                    var controllersData = await client.GetHeatPumpControllersAtLocationAsync(settings.Email!, settings.OctopusPassword!, accountNumber, propertyId.Value);
                    if (controllersData.RootElement.TryGetProperty("data", out var locData)
                        && locData.TryGetProperty("heatPumpControllersAtLocation", out var controllers)
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

            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.AccountNumber == accountNumber, ct);
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

            await db.SaveChangesAsync(ct);

            return Results.Ok(new { deviceId, mpan, serialNumber, euid, propertyId, message = "Device setup complete" });
        }).WithName("SetupHeatPump");

        // Get current telemetry
        group.MapGet("/telemetry/{deviceId}", async (string deviceId, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId, ct);
            if (error is not null)
                return error;

            var telemetry = await client.GetSmartMeterTelemetryAsync(settings!.Email!, settings!.OctopusPassword!, deviceId);
            return Results.Ok(telemetry);
        }).WithName("GetTelemetry");

        // Sync historical data
        group.MapPost("/sync/{deviceId}", async (string deviceId, DateTime? from, DateTime? to,
            IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId, ct);
            if (error is not null)
                return error;

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var consumption = await client.GetConsumptionHistoryAsync(settings!.ApiKey, device!.Mpan!, device.MeterSerialNumber!, from.Value, to.Value);
            var results = consumption.RootElement.GetProperty("results");

            var existing = await db.ConsumptionReadings
                .Where(r => r.DeviceId == deviceId && r.ReadAt >= from && r.ReadAt <= to)
                .Select(r => r.ReadAt)
                .ToListAsync(ct);

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
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { synced = readings.Count, from, to });
        }).WithName("SyncConsumption");

        // Get consumption data
        group.MapGet("/consumption/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, int? skip, int? take, CosyDbContext db, CancellationToken ct) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;
            var safeSkip = Math.Max(skip ?? 0, 0);
            var actualTake = Math.Clamp(take ?? 10000, 1, 50000);

            var query = db.ConsumptionReadings
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.ReadAt >= from && r.ReadAt <= to);

            var totalCount = await query.CountAsync(ct);

            var readings = await query
                .OrderBy(r => r.ReadAt)
                .Skip(safeSkip)
                .Take(actualTake)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                deviceId,
                from,
                to,
                totalCount,
                count = readings.Count,
                hasMore = safeSkip + readings.Count < totalCount,
                readings
            });
        }).WithName("GetConsumption");

        // Get devices
        group.MapGet("/devices", async (CosyDbContext db, CancellationToken ct) =>
        {
            var devices = await db.HeatPumpDevices.AsNoTracking().Where(d => d.IsActive).ToListAsync(ct);
            return Results.Ok(devices);
        }).WithName("GetDevices");

        // Get account properties (needed for heat pump device query)
        group.MapGet("/properties/{accountNumber}", async (string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var viewerData = await client.GetViewerPropertiesAsync(settings!.Email!, settings!.OctopusPassword!);
            var viewer = viewerData.RootElement.GetProperty("data").GetProperty("viewer");
            var accounts = viewer.GetProperty("accounts");
            var account = FindAccount(accounts, accountNumber);

            if (!account.HasValue)
                return Results.NotFound("Account not found in viewer response");

            var properties = account.Value.GetProperty("properties");
            return Results.Ok(properties);
        }).WithName("GetProperties");

        // Get heat pump device info
        group.MapGet("/heatpump-device/{accountNumber}/{propertyId}", async (string accountNumber, int propertyId, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var heatPump = await client.GetHeatPumpDeviceAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, propertyId);
            return Results.Ok(heatPump);
        }).WithName("GetHeatPumpDevice");

        // Get heat pump status
        group.MapGet("/heatpump-status", async (string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var status = await client.GetHeatPumpStatusAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber);
            return Results.Ok(status);
        }).WithName("GetHeatPumpStatus");

        // Get viewer properties with heat pump device details
        group.MapGet("/heatpump-config", async (string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var config = await client.GetViewerPropertiesWithDevicesAsync(settings!.Email!, settings!.OctopusPassword!);
            return Results.Ok(config);
        }).WithName("GetHeatPumpConfig");

        // Get heat pump controller status (uses basic heatPumpStatus query)
        group.MapGet("/heatpump-controller-status", async (string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var status = await client.GetHeatPumpStatusAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber);
            return Results.Ok(status);
        }).WithName("GetHeatPumpControllerStatus");

        // Get heat pump variants
        group.MapGet("/heatpump-variants", async (string accountNumber, string? make, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var variants = await client.GetHeatPumpVariantsAsync(settings!.Email!, settings!.OctopusPassword!, make);
            return Results.Ok(variants);
        }).WithName("GetHeatPumpVariants");

        // Get complete heat pump data (COP, temperatures, performance — uses primary batched query)
        group.MapGet("/heatpump-complete/{accountNumber}/{euid}", async (string accountNumber, string euid, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.AccountNumber == accountNumber, ct);
            if (device is null)
                return Results.NotFound("Device not found for this account");

            var data = await client.GetHeatPumpStatusAndConfigAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, euid);
            return Results.Ok(data);
        }).WithName("GetHeatPumpCompleteData");

        if (app.Environment.IsDevelopment())
        {
            group.MapGet("/introspect/{typeName}", async (string typeName, string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
            {
                var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
                if (error is not null)
                    return error;

                var introspectionQuery = $"{{ __type(name: \"{typeName}\") {{ name kind fields {{ name args {{ name type {{ name kind ofType {{ name kind ofType {{ name kind }} }} }} defaultValue }} type {{ name kind ofType {{ name kind ofType {{ name kind }} }} }} }} }} }}";
                var result = await client.ExecuteRawQueryAsync(settings!.Email!, settings!.OctopusPassword!, introspectionQuery);
                return Results.Ok(result);
            }).WithName("IntrospectType");

            group.MapPost("/graphql", async (GraphqlQueryRequest request, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.AccountNumber))
                    return Results.BadRequest("Account number is required.");

                if (string.IsNullOrWhiteSpace(request.Query))
                    return Results.BadRequest("Query is required.");

                var (settings, error) = await GetSettingsForAccountAsync(db, request.AccountNumber, ct);
                if (error is not null)
                    return error;

                var result = await client.ExecuteRawQueryAsync(settings!.Email!, settings!.OctopusPassword!, request.Query, request.Variables);
                return Results.Ok(result);
            }).WithName("RunGraphqlQuery");
        }

        group.MapGet("/controller-euids/{accountNumber}", async (string accountNumber, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var euids = await client.GetHeatPumpControllerEuidsAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber);
            return Results.Ok(euids);
        }).WithName("GetHeatPumpControllerEuids");

        group.MapGet("/summary/{deviceId}", async (string deviceId, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId, ct);
            if (error is not null)
                return error;

            if (string.IsNullOrWhiteSpace(device!.Euid))
                return Results.Problem("EUID not found for device. Run setup first.");

            var data = await client.GetHeatPumpStatusAndConfigAsync(settings!.Email!, settings!.OctopusPassword!, device.AccountNumber, device.Euid);
            var root = data.RootElement.GetProperty("data");

            var summary = MapHeatPumpSummary(root);
            return Results.Ok(summary);
        }).WithName("GetHeatPumpSummary");

        group.MapGet("/snapshots/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, int? skip, int? take, CosyDbContext db, CancellationToken ct) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;
            var safeSkip = Math.Max(skip ?? 0, 0);
            var actualTake = Math.Clamp(take ?? 10000, 1, 50000);

            var query = db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to);

            var totalCount = await query.CountAsync(ct);

            var snapshots = await query
                .OrderBy(s => s.SnapshotTakenAt)
                .Skip(safeSkip)
                .Take(actualTake)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                deviceId,
                from,
                to,
                totalCount,
                count = snapshots.Count,
                hasMore = safeSkip + snapshots.Count < totalCount,
                snapshots
            });
        }).WithName("GetHeatPumpSnapshots");

        group.MapGet("/snapshots/{deviceId}/latest", async (string deviceId, CosyDbContext db, CancellationToken ct) =>
        {
            var latest = await db.HeatPumpSnapshots
                .Where(s => s.DeviceId == deviceId)
                .OrderByDescending(s => s.SnapshotTakenAt)
                .Select(s => new { s.SnapshotTakenAt })
                .FirstOrDefaultAsync(ct);

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

        group.MapGet("/time-ranged/{accountNumber}/{euid}", async (string accountNumber, string euid, DateTime? from, DateTime? to, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var data = await client.GetHeatPumpTimeRangedPerformanceAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, euid, from.Value, to.Value);
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

        group.MapGet("/time-series/{accountNumber}/{euid}", async (string accountNumber, string euid, DateTime? from, DateTime? to, string? grouping, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
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

            var data = await client.GetHeatPumpTimeSeriesPerformanceAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, euid, from.Value, to.Value, grouping);
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

        group.MapGet("/timeseries/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db, CancellationToken ct) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            if (from > to)
                return Results.BadRequest(new { error = "'from' must be before 'to'." });

            // Normalise to UTC for consistent querying
            var fromUtc = DateTime.SpecifyKind(from.Value, DateTimeKind.Utc);
            var toUtc = DateTime.SpecifyKind(to.Value, DateTimeKind.Utc);

            const int maxRecords = 50000;

            var query = db.HeatPumpTimeSeriesRecords
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.StartAt >= fromUtc && r.StartAt <= toUtc);

            var totalCount = await query.CountAsync(ct);

            var records = await query
                .OrderBy(r => r.StartAt)
                .Take(maxRecords)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                deviceId,
                from = fromUtc,
                to = toUtc,
                totalCount,
                count = records.Count,
                hasMore = totalCount > maxRecords,
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
            IOctopusEnergyClient client, CosyDbContext db, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId, ct);
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
                .ToListAsync(ct);
            var existingSet = new HashSet<DateTime>(existingTimestamps);

            while (chunkStart < to.Value)
            {
                var chunkEnd = chunkStart + chunkSize;
                if (chunkEnd > to.Value)
                    chunkEnd = to.Value;

                try
                {
                    var data = await client.GetHeatPumpTimeSeriesPerformanceAsync(
                        settings!.Email!, settings!.OctopusPassword!, device.AccountNumber, device.Euid, chunkStart, chunkEnd, "MONTH");
                    var root = data.RootElement.GetProperty("data");

                    var chunkSynced = 0;
                    if (root.TryGetProperty("heatPumpTimeSeriesPerformance", out var series)
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
                        await db.SaveChangesAsync(ct);
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

        group.MapGet("/controllers-at-location/{accountNumber}/{propertyId:int}", async (string accountNumber, int propertyId, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var data = await client.GetHeatPumpControllersAtLocationAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, propertyId);
            return Results.Ok(data);
        }).WithName("GetControllersAtLocation");

        // ── Applicable Rates (Tariff) ─────────────────────────────────

        group.MapGet("/rates/{accountNumber}", async (string accountNumber, DateTime? from, DateTime? to, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var device = await db.HeatPumpDevices
                .FirstOrDefaultAsync(d => d.AccountNumber == accountNumber && d.IsActive && d.Mpan != null, ct);
            if (device is null)
                return Results.BadRequest(new { error = "No active device with MPAN found for this account. Run setup first." });

            from ??= DateTime.UtcNow.AddDays(-1);
            to ??= DateTime.UtcNow;

            var data = await client.GetApplicableRatesAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, device.Mpan!, from.Value, to.Value);
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

        group.MapGet("/cost/{accountNumber}", async (string accountNumber, DateTime? from, DateTime? to, IOctopusEnergyClient client, CosyDbContext db, CancellationToken ct) =>
        {
            var (settings, error) = await GetSettingsForAccountAsync(db, accountNumber, ct);
            if (error is not null)
                return error;

            var device = await db.HeatPumpDevices
                .FirstOrDefaultAsync(d => d.AccountNumber == accountNumber && d.IsActive && d.Mpan != null, ct);

            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            var data = await client.GetCostOfUsageAsync(settings!.Email!, settings!.OctopusPassword!, accountNumber, from.Value, to.Value, propertyId: device?.PropertyId, mpxn: device?.Mpan);

            // Surface any GraphQL errors so the UI can display them
            JsonElement? errors = data.RootElement.TryGetProperty("errors", out var errEl) ? errEl : null;

            // data property might be null/missing when errors occur
            JsonElement? root = data.RootElement.TryGetProperty("data", out var dataEl) ? dataEl : null;

            return Results.Ok(new
            {
                accountNumber,
                from,
                to,
                data = root,
                errors
            });
        }).WithName("GetCostOfUsage");

        // ── Stored Cost Data (from background sync) ─────────────────

        group.MapGet("/cost-stored/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db, CancellationToken ct) =>
        {
            var fromDate = DateOnly.FromDateTime(from ?? DateTime.UtcNow.AddDays(-30));
            var toDate = DateOnly.FromDateTime(to ?? DateTime.UtcNow);

            var records = await db.DailyCostRecords
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.Date >= fromDate && r.Date <= toDate)
                .OrderBy(r => r.Date)
                .Select(r => new
                {
                    r.Date,
                    r.TotalCostPence,
                    r.TotalUsageKwh,
                    r.AvgUnitRatePence,
                    r.StandingChargePence,
                    r.UpdatedAt
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                deviceId,
                from = fromDate,
                to = toDate,
                totalDays = records.Count,
                records
            });
        }).WithName("GetStoredCostData");

        // ── Period Summary (server-side aggregation) ────────────────

        group.MapGet("/period-summary/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db, CancellationToken ct) =>
        {
            from ??= DateTime.UtcNow.AddDays(-7);
            to ??= DateTime.UtcNow;

            if (from >= to)
                return Results.BadRequest("'from' must be before 'to'.");
            if ((to.Value - from.Value).TotalDays > 400)
                return Results.BadRequest("Maximum range is 400 days.");

            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .Select(s => new
                {
                    s.CoefficientOfPerformance,
                    s.PowerInputKilowatt,
                    s.HeatOutputKilowatt,
                    s.OutdoorTemperatureCelsius,
                    s.RoomTemperatureCelsius,
                    s.RoomHumidityPercentage,
                    s.HotWaterZoneSetpointCelsius,
                    s.HeatingFlowTemperatureCelsius,
                    s.HeatingZoneHeatDemand,
                    s.HotWaterZoneHeatDemand
                })
                .ToListAsync(ct);

            if (snapshots.Count == 0)
                return Results.Ok(new PeriodSummaryDto { PeriodFrom = from.Value, PeriodTo = to.Value });

            // Single-pass aggregation to avoid multiple list allocations
            double copSum = 0, copMin = double.MaxValue, copMax = double.MinValue;
            int copCount = 0;
            double outdoorSum = 0, outdoorMin = double.MaxValue, outdoorMax = double.MinValue;
            int outdoorCount = 0;
            double roomSum = 0, roomMin = double.MaxValue, roomMax = double.MinValue;
            int roomCount = 0;
            double humiditySum = 0, humidityMin = double.MaxValue, humidityMax = double.MinValue;
            int humidityCount = 0;
            double hwSum = 0, hwMin = double.MaxValue, hwMax = double.MinValue;
            int hwCount = 0;
            double flowSum = 0, flowMin = double.MaxValue, flowMax = double.MinValue;
            int flowCount = 0;
            double totalInputKwh = 0, totalOutputKwh = 0;
            int heatingDemandCount = 0, hotWaterDemandCount = 0;

            foreach (var s in snapshots)
            {
                if (s.CoefficientOfPerformance is > 0)
                {
                    var v = (double)s.CoefficientOfPerformance.Value;
                    copSum += v; copCount++;
                    if (v < copMin) copMin = v;
                    if (v > copMax) copMax = v;
                }
                if (s.PowerInputKilowatt.HasValue)
                    totalInputKwh += (double)s.PowerInputKilowatt.Value * 0.25;
                if (s.HeatOutputKilowatt.HasValue)
                    totalOutputKwh += (double)s.HeatOutputKilowatt.Value * 0.25;
                if (s.OutdoorTemperatureCelsius.HasValue)
                {
                    var v = (double)s.OutdoorTemperatureCelsius.Value;
                    outdoorSum += v; outdoorCount++;
                    if (v < outdoorMin) outdoorMin = v;
                    if (v > outdoorMax) outdoorMax = v;
                }
                if (s.RoomTemperatureCelsius.HasValue)
                {
                    var v = (double)s.RoomTemperatureCelsius.Value;
                    roomSum += v; roomCount++;
                    if (v < roomMin) roomMin = v;
                    if (v > roomMax) roomMax = v;
                }
                if (s.RoomHumidityPercentage.HasValue)
                {
                    var v = (double)s.RoomHumidityPercentage.Value;
                    humiditySum += v; humidityCount++;
                    if (v < humidityMin) humidityMin = v;
                    if (v > humidityMax) humidityMax = v;
                }
                if (s.HotWaterZoneSetpointCelsius.HasValue)
                {
                    var v = (double)s.HotWaterZoneSetpointCelsius.Value;
                    hwSum += v; hwCount++;
                    if (v < hwMin) hwMin = v;
                    if (v > hwMax) hwMax = v;
                }
                if (s.HeatingFlowTemperatureCelsius.HasValue)
                {
                    var v = (double)s.HeatingFlowTemperatureCelsius.Value;
                    flowSum += v; flowCount++;
                    if (v < flowMin) flowMin = v;
                    if (v > flowMax) flowMax = v;
                }
                if (s.HeatingZoneHeatDemand == true) heatingDemandCount++;
                if (s.HotWaterZoneHeatDemand == true) hotWaterDemandCount++;
            }

            var summary = new PeriodSummaryDto
            {
                PeriodFrom = from.Value,
                PeriodTo = to.Value,
                SnapshotCount = snapshots.Count,

                AvgCop = copCount > 0 ? Math.Round(copSum / copCount, 2) : null,
                MinCop = copCount > 0 ? Math.Round(copMin, 2) : null,
                MaxCop = copCount > 0 ? Math.Round(copMax, 2) : null,

                TotalInputKwh = Math.Round(totalInputKwh, 2),
                TotalOutputKwh = Math.Round(totalOutputKwh, 2),

                AvgOutdoorTemp = outdoorCount > 0 ? Math.Round(outdoorSum / outdoorCount, 1) : null,
                MinOutdoorTemp = outdoorCount > 0 ? Math.Round(outdoorMin, 1) : null,
                MaxOutdoorTemp = outdoorCount > 0 ? Math.Round(outdoorMax, 1) : null,

                AvgRoomTemp = roomCount > 0 ? Math.Round(roomSum / roomCount, 1) : null,
                MinRoomTemp = roomCount > 0 ? Math.Round(roomMin, 1) : null,
                MaxRoomTemp = roomCount > 0 ? Math.Round(roomMax, 1) : null,

                AvgRoomHumidity = humidityCount > 0 ? Math.Round(humiditySum / humidityCount, 1) : null,
                MinRoomHumidity = humidityCount > 0 ? Math.Round(humidityMin, 1) : null,
                MaxRoomHumidity = humidityCount > 0 ? Math.Round(humidityMax, 1) : null,

                AvgHotWaterSetpoint = hwCount > 0 ? Math.Round(hwSum / hwCount, 1) : null,
                MinHotWaterSetpoint = hwCount > 0 ? Math.Round(hwMin, 1) : null,
                MaxHotWaterSetpoint = hwCount > 0 ? Math.Round(hwMax, 1) : null,

                AvgFlowTemp = flowCount > 0 ? Math.Round(flowSum / flowCount, 1) : null,
                MinFlowTemp = flowCount > 0 ? Math.Round(flowMin, 1) : null,
                MaxFlowTemp = flowCount > 0 ? Math.Round(flowMax, 1) : null,

                HeatingDutyCyclePercent = Math.Round(100.0 * heatingDemandCount / snapshots.Count, 1),
                HotWaterDutyCyclePercent = Math.Round(100.0 * hotWaterDemandCount / snapshots.Count, 1)
            };

            return Results.Ok(summary);
        }).WithName("GetPeriodSummary");

        // ── Daily Aggregates ─────────────────────────────────────────

        group.MapGet("/daily-aggregates/{deviceId}", async (string deviceId, DateTime? from, DateTime? to, CosyDbContext db, IHeatPumpDataService dataService, CancellationToken ct) =>
        {
            from ??= DateTime.UtcNow.AddDays(-30);
            to ??= DateTime.UtcNow;

            // Cap at 366 days to prevent loading unbounded data into memory.
            // At 15-min intervals, 366 days = ~35,000 snapshots which is manageable.
            var maxSpan = TimeSpan.FromDays(366);
            if (to.Value - from.Value > maxSpan)
                from = to.Value - maxSpan;

            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .OrderBy(s => s.SnapshotTakenAt)
                .ToListAsync(ct);

            var aggregates = dataService.ComputeDailyAggregates(snapshots);

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
            IAiAnalysisService aiService, IOctopusEnergyClient octopusClient, IHeatPumpDataService dataService, CosyDbContext db,
            ILogger<AiAnalysisService> logger, CancellationToken ct) =>
        {
            if (request.From >= request.To)
                return Results.BadRequest(new { error = "From date must be before To date." });

            if ((request.To - request.From).TotalDays > 365)
                return Results.BadRequest(new { error = "Date range must not exceed 365 days." });

            var device = await db.HeatPumpDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
            if (device is null)
                return Results.NotFound("Device not found");

            var from = request.From;
            var to = request.To;

            // Load snapshots (for weather comp, flow temp, room temp, zones, COP, etc.)
            var snapshots = await db.HeatPumpSnapshots
                .AsNoTracking()
                .Where(s => s.DeviceId == deviceId && s.SnapshotTakenAt >= from && s.SnapshotTakenAt <= to)
                .OrderBy(s => s.SnapshotTakenAt)
                .ToListAsync(ct);

            // Load time series history (synced hourly energy data)
            var timeSeriesRecords = await db.HeatPumpTimeSeriesRecords
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.StartAt >= from && r.StartAt <= to)
                .OrderBy(r => r.StartAt)
                .ToListAsync(ct);

            // Load account settings once — used by both auto-sync and cost data fetch
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber, ct);

            // Auto-sync time series if coverage is sparse (for new installs or first-time analysis)
            var requestedDays = (int)(to - from).TotalDays;
            var coveredDays = timeSeriesRecords
                .Select(r => DateOnly.FromDateTime(r.StartAt))
                .Distinct()
                .Count();

            if (coveredDays < requestedDays * 0.5 && !string.IsNullOrWhiteSpace(device.Euid))
            {
                if (settings is not null)
                {
                    var syncFrom = from;
                    var syncTo = to;
                    // Cap auto-sync to 90 days to avoid slow requests
                    if ((syncTo - syncFrom).TotalDays > 90)
                        syncFrom = syncTo.AddDays(-90);

                    logger.LogInformation("Auto-syncing time series for device {DeviceId} from {From} to {To} (had {Covered}/{Requested} days)",
                        deviceId, syncFrom, syncTo, coveredDays, requestedDays);

                    try
                    {
                        var existingTimestamps = await db.HeatPumpTimeSeriesRecords
                            .Where(r => r.DeviceId == deviceId && r.StartAt >= syncFrom && r.StartAt <= syncTo)
                            .Select(r => r.StartAt)
                            .ToListAsync(ct);
                        var existingSet = new HashSet<DateTime>(existingTimestamps);

                        var chunkStart = syncFrom;
                        var chunkSize = TimeSpan.FromDays(60);

                        while (chunkStart < syncTo)
                        {
                            var chunkEnd = chunkStart + chunkSize;
                            if (chunkEnd > syncTo) chunkEnd = syncTo;

                            try
                            {
                                var tsData = await octopusClient.GetHeatPumpTimeSeriesPerformanceAsync(
                                    settings.Email!, settings.OctopusPassword!, device.AccountNumber, device.Euid, chunkStart, chunkEnd, "MONTH");
                                var tsRoot = tsData.RootElement.GetProperty("data");

                                if (tsRoot.TryGetProperty("heatPumpTimeSeriesPerformance", out var tsSeries)
                                    && tsSeries.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in tsSeries.EnumerateArray())
                                    {
                                        if (!item.TryGetProperty("startAt", out var startAtEl)
                                            || !DateTimeOffset.TryParse(startAtEl.GetString(),
                                                System.Globalization.CultureInfo.InvariantCulture,
                                                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                                                out var startAtDto))
                                            continue;

                                        var startAt = startAtDto.UtcDateTime;
                                        if (existingSet.Contains(startAt)) continue;

                                        var endAtUtc = startAt.AddHours(1);
                                        if (item.TryGetProperty("endAt", out var endAtEl)
                                            && DateTimeOffset.TryParse(endAtEl.GetString(),
                                                System.Globalization.CultureInfo.InvariantCulture,
                                                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                                                out var endAtDto))
                                            endAtUtc = endAtDto.UtcDateTime;

                                        var record = new HeatPumpTimeSeriesRecord
                                        {
                                            DeviceId = deviceId,
                                            StartAt = startAt,
                                            EndAt = endAtUtc,
                                            CreatedAt = DateTime.UtcNow
                                        };

                                        if (item.TryGetProperty("energyInput", out var ei) && ei.TryGetProperty("value", out var eiVal)
                                            && decimal.TryParse(eiVal.GetString(), System.Globalization.NumberStyles.Any,
                                                System.Globalization.CultureInfo.InvariantCulture, out var eiDec))
                                            record.EnergyInputKwh = eiDec;

                                        if (item.TryGetProperty("energyOutput", out var eo) && eo.TryGetProperty("value", out var eoVal)
                                            && decimal.TryParse(eoVal.GetString(), System.Globalization.NumberStyles.Any,
                                                System.Globalization.CultureInfo.InvariantCulture, out var eoDec))
                                            record.EnergyOutputKwh = eoDec;

                                        if (item.TryGetProperty("outdoorTemperature", out var ot) && ot.TryGetProperty("value", out var otVal)
                                            && decimal.TryParse(otVal.GetString(), System.Globalization.NumberStyles.Any,
                                                System.Globalization.CultureInfo.InvariantCulture, out var otDec))
                                            record.OutdoorTemperatureCelsius = otDec;

                                        db.HeatPumpTimeSeriesRecords.Add(record);
                                        existingSet.Add(startAt);
                                    }

                                    await db.SaveChangesAsync(ct);
                                    db.ChangeTracker.Clear();
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed auto-sync time-series chunk {From} to {To} for device {DeviceId}",
                                    chunkStart, chunkEnd, deviceId);
                            }

                            chunkStart = chunkEnd;
                        }

                        // Reload time series after sync
                        timeSeriesRecords = await db.HeatPumpTimeSeriesRecords
                            .AsNoTracking()
                            .Where(r => r.DeviceId == deviceId && r.StartAt >= from && r.StartAt <= to)
                            .OrderBy(r => r.StartAt)
                            .ToListAsync(ct);

                        logger.LogInformation("Auto-sync complete for device {DeviceId}: now have {Count} time series records",
                            deviceId, timeSeriesRecords.Count);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Auto-sync time series failed for device {DeviceId}", deviceId);
                    }
                }
            }

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
                ? dataService.ComputeDailyAggregates(snapshots)
                : new List<DailyAggregateDto>();

            // Enrich with time series data + weather comp from nearest snapshots
            if (timeSeriesRecords.Count > 0)
            {
                dataService.EnrichAggregatesWithTimeSeries(aggregates, timeSeriesRecords, snapshots);
            }

            // Merge cost data — prefer stored DB records, fall back to live API
            var costDataStatus = "No account settings found";
            var costFromDate = DateOnly.FromDateTime(from);
            var costToDate = DateOnly.FromDateTime(to);
            var storedCosts = await db.DailyCostRecords
                .AsNoTracking()
                .Where(r => r.DeviceId == deviceId && r.Date >= costFromDate && r.Date <= costToDate)
                .ToDictionaryAsync(r => r.Date, ct);

            if (storedCosts.Count > 0)
            {
                var mergedCount = 0;
                foreach (var agg in aggregates)
                {
                    if (storedCosts.TryGetValue(agg.Date, out var cost))
                    {
                        agg.DailyCostPence = cost.TotalCostPence;
                        agg.DailyUsageKwh = cost.TotalUsageKwh;
                        agg.AvgUnitRatePence = cost.AvgUnitRatePence;
                        agg.CostPerKwhHeatPence = agg.TotalHeatOutputKwh > 0
                            ? cost.TotalCostPence / agg.TotalHeatOutputKwh
                            : null;
                        mergedCount++;
                    }
                }
                costDataStatus = $"OK (stored): {storedCosts.Count} days of cost data, merged into {mergedCount} aggregates";
                logger.LogInformation("Merged stored cost data for device {DeviceId}: {Status}", deviceId, costDataStatus);
            }
            else if (settings is not null)
            {
                // Fall back to live API if no stored data
                try
                    {
                        logger.LogInformation("No stored cost data — fetching live from Octopus API for account {Account} from {From} to {To}",
                            device.AccountNumber, from, to);

                        var costData = await octopusClient.GetCostOfUsageAsync(settings.Email!, settings.OctopusPassword!, device.AccountNumber, from, to, propertyId: device.PropertyId, mpxn: device.Mpan);

                        var costRoot = costData.RootElement.GetProperty("data");

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
                                var startAtStr = TryGetString(node, "startAt", "fromDatetime", "from");
                                if (startAtStr == null || !DateTime.TryParse(startAtStr, out var dt)) continue;

                                var date = DateOnly.FromDateTime(dt);
                                var cost = TryGetDouble(node, "costInclTax", "totalCost", "cost");
                                var usage = TryGetDouble(node, "consumptionKwh", "totalConsumption", "consumption");
                                var unitRate = TryGetDouble(node, "unitRateInclTax", "unitRate");

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

                            costDataStatus = $"OK (live): {costByDate.Count} days of cost data, merged into {mergedCount} aggregates";
                            logger.LogInformation("Merged live cost data for device {DeviceId}: {Status}", deviceId, costDataStatus);
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

            var analysis = await aiService.AnalyseAsync(aggregates, request.Question, settings?.AnthropicApiKey, ct);

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

        // AI Dashboard Summary (auto-cached 30 min, reuses daily aggregate pipeline)
        group.MapGet("/ai-summary/{deviceId}", async (string deviceId, IAiAnalysisService aiService, CosyDbContext db, CancellationToken ct) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId, ct);
            if (error is not null)
                return error;

            var anthropicKey = settings?.AnthropicApiKey;
            var summary = await aiService.GenerateDashboardSummaryAsync(deviceId, anthropicApiKey: anthropicKey);
            return Results.Ok(summary);
        }).WithName("GetAiSummary");

        group.MapGet("/ai-summary/{deviceId}/refresh", async (string deviceId, IAiAnalysisService aiService, CosyDbContext db, CancellationToken ct) =>
        {
            var (device, settings, error) = await GetDeviceAndSettingsAsync(db, deviceId, ct);
            if (error is not null)
                return error;

            var anthropicKey = settings?.AnthropicApiKey;
            var summary = await aiService.GenerateDashboardSummaryAsync(deviceId, forceRefresh: true, anthropicApiKey: anthropicKey);
            return Results.Ok(summary);
        }).WithName("RefreshAiSummary");
    }

    // Business logic for daily aggregates and time series enrichment
    // is in HeatPumpDataService (injected via IHeatPumpDataService).

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
        if (!data.TryGetProperty("heatPumpControllerStatus", out var status))
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
        if (!data.TryGetProperty("heatPumpControllerConfiguration", out var configuration))
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
        if (!data.TryGetProperty("heatPumpTimeSeriesPerformance", out var liveArray)
            || liveArray.ValueKind != JsonValueKind.Array
            || liveArray.GetArrayLength() == 0)
            return null;

        // Take the most recent bucket (last element in the LIVE time series)
        var live = liveArray.EnumerateArray().Last();

        // COP is not returned by the new API — compute client-side from energyOutput / energyInput
        string? cop = null;
        if (live.TryGetProperty("energyInput", out var eiEl) && live.TryGetProperty("energyOutput", out var eoEl))
        {
            var eIn = GetDecimal(eiEl, "value");
            var eOut = GetDecimal(eoEl, "value");
            if (eIn is > 0 && eOut.HasValue)
                cop = (eOut.Value / eIn.Value).ToString("F2");
        }

        return new HeatPumpLivePerformance
        {
            CoefficientOfPerformance = cop,
            OutdoorTemperature = MapValueAndUnit(live, "outdoorTemperature"),
            HeatOutput = MapValueAndUnit(live, "energyOutput"),
            PowerInput = MapValueAndUnit(live, "energyInput"),
            ReadAt = GetString(live, "startAt")
        };
    }

    private static HeatPumpLifetimePerformance? MapLifetimePerformance(JsonElement data)
    {
        if (!data.TryGetProperty("heatPumpLifetimePerformance", out var lifetime))
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

    internal static string? TryGetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }
        return null;
    }

    internal static double TryGetDouble(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d))
                return d;

            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return 0;
    }

    public sealed record GraphqlQueryRequest(string AccountNumber, string Query, JsonElement? Variables);
}

