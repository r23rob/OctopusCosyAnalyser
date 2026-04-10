using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;
using Microsoft.EntityFrameworkCore;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class HeatPumpTimeSeriesSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeatPumpTimeSeriesSyncWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromHours(6);

    public HeatPumpTimeSeriesSyncWorker(IServiceProvider serviceProvider, ILogger<HeatPumpTimeSeriesSyncWorker> logger)
    {
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

    private async Task SyncAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
        var graphqlService = scope.ServiceProvider.GetRequiredService<IOctopusGraphQLService>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive && d.Euid != null && d.Euid != "")
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Time series sync for {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SyncDeviceAsync(db, graphqlService, device, stoppingToken);
        }
    }

    private async Task SyncDeviceAsync(
        CosyDbContext db, IOctopusGraphQLService graphqlService,
        HeatPumpDevice device, CancellationToken stoppingToken)
    {
        var settings = await db.OctopusAccountSettings
            .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber, stoppingToken);

        if (settings is null)
        {
            _logger.LogWarning("No settings found for account {Account}, skipping time series sync for device {DeviceId}",
                device.AccountNumber, device.DeviceId);
            return;
        }

        try
        {
            // Sync last 2 days of hourly data (DAY grouping max span)
            var from = DateTime.UtcNow.AddDays(-2);
            var to = DateTime.UtcNow;

            var existing = await db.HeatPumpTimeSeriesRecords
                .Where(r => r.DeviceId == device.DeviceId && r.StartAt >= from)
                .Select(r => r.StartAt)
                .ToListAsync(stoppingToken);
            var existingSet = new HashSet<DateTime>(existing);

            var entries = await graphqlService.GetHeatPumpTimeSeriesPerformanceAsync(
                settings, device.AccountNumber, device.Euid!, from, to, "DAY", stoppingToken);

            var synced = MapAndPersistTimeSeriesEntries(entries, device.DeviceId, existingSet, db);

            if (synced > 0)
                await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Time series sync for device {DeviceId}: {Synced} new records", device.DeviceId, synced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync time series for device {DeviceId}", device.DeviceId);
        }
    }

    /// <summary>
    /// Maps typed TimeSeriesEntry responses to HeatPumpTimeSeriesRecord entities and adds to the DbContext.
    /// Shared utility for time-series sync operations.
    /// </summary>
    internal static int MapAndPersistTimeSeriesEntries(
        TimeSeriesEntry?[]? entries, string deviceId,
        HashSet<DateTime> existingSet, CosyDbContext db)
    {
        if (entries is null)
            return 0;

        var synced = 0;
        foreach (var entry in entries)
        {
            if (entry is null) continue;

            var startAt = entry.StartAt.UtcDateTime;
            if (existingSet.Contains(startAt))
                continue;

            var record = new HeatPumpTimeSeriesRecord
            {
                DeviceId = deviceId,
                StartAt = startAt,
                EndAt = entry.EndAt.UtcDateTime,
                EnergyInputKwh = entry.EnergyInput?.Value,
                EnergyOutputKwh = entry.EnergyOutput?.Value,
                OutdoorTemperatureCelsius = entry.OutdoorTemperature?.Value,
                CreatedAt = DateTime.UtcNow
            };

            db.HeatPumpTimeSeriesRecords.Add(record);
            existingSet.Add(startAt);
            synced++;
        }

        return synced;
    }
}
