using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;
using Microsoft.EntityFrameworkCore;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class HeatPumpSnapshotWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeatPumpSnapshotWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(Constants.SnapshotIntervalMinutes);

    public HeatPumpSnapshotWorker(IServiceProvider serviceProvider, ILogger<HeatPumpSnapshotWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heat Pump Snapshot Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SnapshotAllDevicesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during heat pump snapshot");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Heat Pump Snapshot Worker stopped");
    }

    private async Task SnapshotAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
        var graphqlService = scope.ServiceProvider.GetRequiredService<IOctopusGraphQLService>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Snapshotting {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SnapshotDeviceAsync(db, graphqlService, device, stoppingToken);
        }

        await db.SaveChangesAsync(stoppingToken);
    }

    private async Task SnapshotDeviceAsync(
        CosyDbContext db, IOctopusGraphQLService graphqlService,
        HeatPumpDevice device, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(device.Euid))
        {
            _logger.LogWarning("Device {DeviceId} has no EUID, skipping snapshot", device.DeviceId);
            return;
        }

        var settings = await db.OctopusAccountSettings
            .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber, stoppingToken);

        if (settings is null)
        {
            _logger.LogWarning("No settings found for account {Account}, skipping device {DeviceId}", device.AccountNumber, device.DeviceId);
            return;
        }

        try
        {
            var response = await graphqlService.GetHeatPumpStatusAndConfigAsync(
                settings, device.AccountNumber, device.Euid, stoppingToken);

            if (response is null)
            {
                _logger.LogWarning("No data returned for device {DeviceId}", device.DeviceId);
                return;
            }

            var snapshot = SnapshotMapper.MapFromStatusAndConfig(response, device.DeviceId, device.AccountNumber);
            if (snapshot is null)
            {
                _logger.LogWarning("Failed to map snapshot for device {DeviceId}", device.DeviceId);
                return;
            }

            db.HeatPumpSnapshots.Add(snapshot);
            _logger.LogInformation("Snapshotted device {DeviceId}: COP={COP}", device.DeviceId, snapshot.CoefficientOfPerformance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to snapshot device {DeviceId}", device.DeviceId);
        }
    }
}
