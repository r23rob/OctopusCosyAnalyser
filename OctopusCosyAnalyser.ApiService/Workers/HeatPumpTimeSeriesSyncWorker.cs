using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class HeatPumpTimeSeriesSyncWorker : BackgroundService
{
    private readonly ICosyDataStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeatPumpTimeSeriesSyncWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(Constants.SnapshotIntervalMinutes);

    // ICosyDataStore is a singleton and safe to inject directly. IOctopusGraphQLService is
    // Scoped, so we still need a scope factory to resolve it safely from a Singleton-lifetime
    // BackgroundService (registered via AddHostedService in local continuous mode).
    public HeatPumpTimeSeriesSyncWorker(ICosyDataStore store, IServiceProvider serviceProvider, ILogger<HeatPumpTimeSeriesSyncWorker> logger)
    {
        _store = store;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heat Pump Time Series Sync Worker started");

        // Stagger startup to avoid concurrent API calls with other workers
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllDevicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during time series sync");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Heat Pump Time Series Sync Worker stopped");
    }

    /// <summary>
    /// Run-once entry point used by ACA Jobs / scheduled runners.
    /// </summary>
    public Task RunOnceAsync(CancellationToken ct) => SyncAllDevicesAsync(ct);

    private async Task SyncAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var graphqlService = scope.ServiceProvider.GetRequiredService<IOctopusGraphQLService>();

        // Workers run with no user context — list across all owners.
        var devices = (await _store.ListAllActiveDevicesAsync(stoppingToken))
            .Where(d => !string.IsNullOrEmpty(d.Euid))
            .ToList();

        _logger.LogInformation("Time series sync for {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SyncDeviceAsync(graphqlService, device, stoppingToken);
        }
    }

    private async Task SyncDeviceAsync(
        IOctopusGraphQLService graphqlService,
        HeatPumpDevice device, CancellationToken stoppingToken)
    {
        var settings = await _store.GetSettingsAsync(device.OwnerId!, device.AccountNumber, stoppingToken);

        if (settings is null)
        {
            _logger.LogWarning("No settings found for owner {Owner} / account {Account}, skipping time series sync for device {DeviceId}",
                device.OwnerId, device.AccountNumber, device.DeviceId);
            return;
        }

        try
        {
            // Sync last 2 days of hourly data (DAY grouping max span)
            var from = DateTime.UtcNow.AddDays(-2);
            var to = DateTime.UtcNow;

            var existingSet = await _store.GetTimeSeriesTimestampsAsync(device.DeviceId, from, stoppingToken);

            var entries = await graphqlService.GetHeatPumpTimeSeriesPerformanceAsync(
                settings, device.AccountNumber, device.Euid!, from, to, "DAY", stoppingToken);

            var newRecords = MapTimeSeriesEntries(entries, device.DeviceId, existingSet, device.OwnerId);

            if (newRecords.Count > 0)
                await _store.PutTimeSeriesBatchAsync(newRecords, stoppingToken);

            _logger.LogInformation("Time series sync for device {DeviceId}: {Synced} new records", device.DeviceId, newRecords.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync time series for device {DeviceId}", device.DeviceId);
        }
    }

    /// <summary>
    /// Maps typed TimeSeriesEntry responses to HeatPumpTimeSeriesRecord entities, skipping any
    /// timestamps already present in <paramref name="existingSet"/>. Shared utility for
    /// time-series sync operations.
    ///
    /// <para>
    /// Workers (no HttpContext) MUST pass ownerId — there is no current user for the
    /// data store to stamp from. Endpoint callers (with HttpContext) may pass null and
    /// stamp OwnerId from the authenticated user themselves.
    /// </para>
    /// </summary>
    internal static List<HeatPumpTimeSeriesRecord> MapTimeSeriesEntries(
        TimeSeriesEntry?[]? entries, string deviceId,
        HashSet<DateTime> existingSet, string? ownerId = null)
    {
        var records = new List<HeatPumpTimeSeriesRecord>();

        if (entries is null)
            return records;

        foreach (var entry in entries)
        {
            if (entry is null) continue;

            var startAt = entry.StartAt.UtcDateTime;
            if (existingSet.Contains(startAt))
                continue;

            var record = new HeatPumpTimeSeriesRecord
            {
                OwnerId = ownerId,
                DeviceId = deviceId,
                StartAt = startAt,
                EndAt = entry.EndAt.UtcDateTime,
                EnergyInputKwh = entry.EnergyInput?.Value,
                EnergyOutputKwh = entry.EnergyOutput?.Value,
                OutdoorTemperatureCelsius = entry.OutdoorTemperature?.Value,
                CreatedAt = DateTime.UtcNow
            };

            records.Add(record);
            existingSet.Add(startAt);
        }

        return records;
    }
}
