using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

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
        var client = scope.ServiceProvider.GetRequiredService<OctopusEnergyClient>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive && d.Euid != null && d.Euid != "")
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Time series sync for {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SyncDeviceAsync(db, client, device, stoppingToken);
        }
    }

    private async Task SyncDeviceAsync(CosyDbContext db, OctopusEnergyClient client, HeatPumpDevice device, CancellationToken stoppingToken)
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

            var data = await client.GetHeatPumpTimeSeriesPerformanceAsync(
                settings.ApiKey, device.AccountNumber, device.Euid!, from, to, "DAY");
            var root = data.RootElement.GetProperty("data");

            var synced = 0;
            if (root.TryGetProperty("heatPumpTimeSeriesPerformance", out var series)
                && series.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in series.EnumerateArray())
                {
                    if (!item.TryGetProperty("startAt", out var startAtEl)
                        || !DateTimeOffset.TryParse(startAtEl.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var startAtDto))
                        continue;

                    var startAt = startAtDto.UtcDateTime;

                    if (existingSet.Contains(startAt))
                        continue;

                    var endAtUtc = startAt.AddHours(1); // default for hourly buckets
                    if (item.TryGetProperty("endAt", out var endAtEl)
                        && DateTimeOffset.TryParse(endAtEl.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var endAtDto))
                        endAtUtc = endAtDto.UtcDateTime;

                    var record = new HeatPumpTimeSeriesRecord
                    {
                        DeviceId = device.DeviceId,
                        StartAt = startAt,
                        EndAt = endAtUtc,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (item.TryGetProperty("energyInput", out var ei) && ei.TryGetProperty("value", out var eiVal)
                        && decimal.TryParse(eiVal.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var eiDec))
                        record.EnergyInputKwh = eiDec;

                    if (item.TryGetProperty("energyOutput", out var eo) && eo.TryGetProperty("value", out var eoVal)
                        && decimal.TryParse(eoVal.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var eoDec))
                        record.EnergyOutputKwh = eoDec;

                    if (item.TryGetProperty("outdoorTemperature", out var ot) && ot.TryGetProperty("value", out var otVal)
                        && decimal.TryParse(otVal.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var otDec))
                        record.OutdoorTemperatureCelsius = otDec;

                    db.HeatPumpTimeSeriesRecords.Add(record);
                    existingSet.Add(startAt);
                    synced++;
                }
            }

            if (synced > 0)
                await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Time series sync for device {DeviceId}: {Synced} new records", device.DeviceId, synced);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync time series for device {DeviceId}", device.DeviceId);
        }
    }
}
