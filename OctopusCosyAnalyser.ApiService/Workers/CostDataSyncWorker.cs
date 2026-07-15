using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Helpers;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class CostDataSyncWorker : BackgroundService
{
    private readonly ICosyDataStore _store;
    private readonly IOctopusEnergyClient _client;
    private readonly ILogger<CostDataSyncWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromHours(6);

    // ICosyDataStore is a singleton. IOctopusEnergyClient is a typed HttpClient registered via
    // AddHttpClient (Transient), so it's also safe to inject directly — no scope needed.
    public CostDataSyncWorker(ICosyDataStore store, IOctopusEnergyClient client, ILogger<CostDataSyncWorker> logger)
    {
        _store = store;
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cost Data Sync Worker started");

        // Stagger startup to avoid concurrent API calls with other workers
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

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

    /// <summary>
    /// Run-once entry point used by ACA Jobs / scheduled runners.
    /// </summary>
    public Task RunOnceAsync(CancellationToken ct) => SyncAllDevicesAsync(ct);

    private async Task SyncAllDevicesAsync(CancellationToken stoppingToken)
    {
        // Workers run with no user context — list across all owners.
        var devices = await _store.ListAllActiveDevicesAsync(stoppingToken);

        _logger.LogInformation("Cost data sync for {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            stoppingToken.ThrowIfCancellationRequested();
            await SyncDeviceAsync(device, stoppingToken);
        }
    }

    private async Task SyncDeviceAsync(HeatPumpDevice device, CancellationToken stoppingToken)
    {
        var settings = await _store.GetSettingsAsync(device.OwnerId!, device.AccountNumber, stoppingToken);

        if (settings is null)
        {
            _logger.LogWarning("No settings found for owner {Owner} / account {Account}, skipping cost sync for device {DeviceId}",
                device.OwnerId, device.AccountNumber, device.DeviceId);
            return;
        }

        try
        {
            // Check if this device has any existing cost records to determine backfill range
            var hasExisting = await _store.HasAnyCostDataAsync(device.DeviceId, stoppingToken);

            // First run: backfill 90 days. Subsequent runs: sync last 7 days to catch late data.
            var daysToSync = hasExisting ? 7 : 90;
            var from = DateTime.UtcNow.AddDays(-daysToSync);
            var to = DateTime.UtcNow;

            _logger.LogInformation("Syncing cost data for device {DeviceId}: {Days} days (backfill={IsBackfill})",
                device.DeviceId, daysToSync, !hasExisting);

            var costData = await _client.GetCostOfUsageAsync(
                settings, device.AccountNumber, from, to,
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
            var costByDate = new Dictionary<DateOnly, (decimal cost, decimal usage, decimal unitRate, decimal? standingCharge)>();
            foreach (var edge in edges.EnumerateArray())
            {
                if (!edge.TryGetProperty("node", out var node)) continue;

                var startAtStr = JsonHelpers.TryGetString(node, "startAt", "fromDatetime", "from");
                if (startAtStr == null || !DateTime.TryParse(startAtStr, out var dt)) continue;

                var date = DateOnly.FromDateTime(dt);
                var cost = (decimal)JsonHelpers.TryGetDouble(node, "costInclTax", "totalCost", "cost");
                var usage = (decimal)JsonHelpers.TryGetDouble(node, "consumptionKwh", "totalConsumption", "consumption");
                var unitRate = (decimal)JsonHelpers.TryGetDouble(node, "unitRateInclTax", "unitRate");
                var standingCharge = node.TryGetProperty("standingCharge", out var scEl) && scEl.TryGetDouble(out var scVal) ? (decimal)scVal : (decimal?)null;

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

            // DynamoDB PutItem is an upsert by nature — no need to distinguish insert vs update.
            var records = costByDate.Select(kv => new DailyCostRecord
            {
                OwnerId = device.OwnerId,
                DeviceId = device.DeviceId,
                Date = kv.Key,
                TotalCostPence = kv.Value.cost,
                TotalUsageKwh = kv.Value.usage,
                AvgUnitRatePence = kv.Value.unitRate,
                StandingChargePence = kv.Value.standingCharge,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            if (records.Count > 0)
                await _store.UpsertDailyCostBatchAsync(records, stoppingToken);

            _logger.LogInformation("Cost data sync for device {DeviceId}: {Count} days upserted", device.DeviceId, records.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync cost data for device {DeviceId}", device.DeviceId);
        }
    }
}
