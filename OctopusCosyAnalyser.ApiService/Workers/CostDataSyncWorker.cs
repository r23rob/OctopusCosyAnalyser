using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Endpoints;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class CostDataSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CostDataSyncWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromHours(6);

    public CostDataSyncWorker(IServiceProvider serviceProvider, ILogger<CostDataSyncWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cost Data Sync Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllDevicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cost data sync");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Cost Data Sync Worker stopped");
    }

    private async Task SyncAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<OctopusEnergyClient>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Cost data sync for {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await SyncDeviceAsync(db, client, device, stoppingToken);
        }
    }

    private async Task SyncDeviceAsync(CosyDbContext db, OctopusEnergyClient client, HeatPumpDevice device, CancellationToken stoppingToken)
    {
        var settings = await db.OctopusAccountSettings
            .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber, stoppingToken);

        if (settings is null)
        {
            _logger.LogWarning("No settings found for account {Account}, skipping cost sync for device {DeviceId}",
                device.AccountNumber, device.DeviceId);
            return;
        }

        try
        {
            // Check if this device has any existing cost records to determine backfill range
            var hasExisting = await db.DailyCostRecords
                .AnyAsync(r => r.DeviceId == device.DeviceId, stoppingToken);

            // First run: backfill 90 days. Subsequent runs: sync last 7 days to catch late data.
            var daysToSync = hasExisting ? 7 : 90;
            var from = DateTime.UtcNow.AddDays(-daysToSync);
            var to = DateTime.UtcNow;

            _logger.LogInformation("Syncing cost data for device {DeviceId}: {Days} days (backfill={IsBackfill})",
                device.DeviceId, daysToSync, !hasExisting);

            var costData = await client.GetCostOfUsageAsync(
                settings.ApiKey, device.AccountNumber, from, to,
                propertyId: device.PropertyId, mpxn: device.Mpan);

            // Check for GraphQL errors
            if (costData.RootElement.TryGetProperty("errors", out var errorsEl)
                && errorsEl.ValueKind == JsonValueKind.Array
                && errorsEl.GetArrayLength() > 0)
            {
                var errorMsgs = string.Join("; ", errorsEl.EnumerateArray()
                    .Select(e => e.TryGetProperty("message", out var m) ? m.GetString() : e.ToString()));
                _logger.LogWarning("Cost data query returned errors for device {DeviceId}: {Errors}", device.DeviceId, errorMsgs);
                return;
            }

            var costRoot = costData.RootElement.GetProperty("data");

            if (!costRoot.TryGetProperty("costOfUsage", out var costEl)
                || costEl.ValueKind == JsonValueKind.Null
                || !costEl.TryGetProperty("edges", out var edges)
                || edges.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Cost data query returned null/empty for device {DeviceId}", device.DeviceId);
                return;
            }

            // Aggregate half-hourly cost data into daily totals
            var costByDate = new Dictionary<DateOnly, (double cost, double usage, double unitRate, double? standingCharge)>();
            foreach (var edge in edges.EnumerateArray())
            {
                if (!edge.TryGetProperty("node", out var node)) continue;

                var startAtStr = HeatPumpEndpoints.TryGetString(node, "startAt", "fromDatetime", "from");
                if (startAtStr == null || !DateTime.TryParse(startAtStr, out var dt)) continue;

                var date = DateOnly.FromDateTime(dt);
                var cost = HeatPumpEndpoints.TryGetDouble(node, "costInclTax", "totalCost", "cost");
                var usage = HeatPumpEndpoints.TryGetDouble(node, "consumptionKwh", "totalConsumption", "consumption");
                var unitRate = HeatPumpEndpoints.TryGetDouble(node, "unitRateInclTax", "unitRate");
                var standingCharge = node.TryGetProperty("standingCharge", out var scEl) && scEl.TryGetDouble(out var scVal) ? scVal : (double?)null;

                if (costByDate.TryGetValue(date, out var existing))
                {
                    var newCost = existing.cost + cost;
                    var newUsage = existing.usage + usage;
                    var newUnitRate = newUsage > 0 ? newCost / newUsage : existing.unitRate;
                    var newStanding = standingCharge ?? existing.standingCharge;
                    costByDate[date] = (newCost, newUsage, newUnitRate, newStanding);
                }
                else
                {
                    costByDate[date] = (cost, usage, unitRate, standingCharge);
                }
            }

            // Upsert into database
            var existingRecords = await db.DailyCostRecords
                .Where(r => r.DeviceId == device.DeviceId && r.Date >= DateOnly.FromDateTime(from))
                .ToDictionaryAsync(r => r.Date, stoppingToken);

            var upserted = 0;
            foreach (var (date, data) in costByDate)
            {
                if (existingRecords.TryGetValue(date, out var record))
                {
                    record.TotalCostPence = data.cost;
                    record.TotalUsageKwh = data.usage;
                    record.AvgUnitRatePence = data.unitRate;
                    record.StandingChargePence = data.standingCharge;
                    record.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    db.DailyCostRecords.Add(new DailyCostRecord
                    {
                        DeviceId = device.DeviceId,
                        Date = date,
                        TotalCostPence = data.cost,
                        TotalUsageKwh = data.usage,
                        AvgUnitRatePence = data.unitRate,
                        StandingChargePence = data.standingCharge,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                upserted++;
            }

            if (upserted > 0)
                await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Cost data sync for device {DeviceId}: {Count} days upserted", device.DeviceId, upserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync cost data for device {DeviceId}", device.DeviceId);
        }
    }
}
