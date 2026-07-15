using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.GraphQL;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class HeatPumpSnapshotWorker : BackgroundService
{
    private readonly ICosyDataStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeatPumpSnapshotWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(Constants.SnapshotIntervalMinutes);

    // ICosyDataStore is a singleton and safe to inject directly. IOctopusGraphQLService is
    // Scoped (it depends on per-request GraphQL client wiring), so we still need a scope
    // factory to resolve it — otherwise a Singleton-lifetime BackgroundService (registered via
    // AddHostedService in local continuous mode) would capture a scoped dependency forever.
    public HeatPumpSnapshotWorker(ICosyDataStore store, IServiceProvider serviceProvider, ILogger<HeatPumpSnapshotWorker> logger)
    {
        _store = store;
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

    /// <summary>
    /// Run-once entry point used by ACA Jobs / scheduled runners.
    /// Exits after one full pass over all active devices.
    /// </summary>
    public Task RunOnceAsync(CancellationToken ct) => SnapshotAllDevicesAsync(ct);

    private async Task SnapshotAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var graphqlService = scope.ServiceProvider.GetRequiredService<IOctopusGraphQLService>();

        // Workers run with no user context — list across all owners.
        var devices = await _store.ListAllActiveDevicesAsync(stoppingToken);

        _logger.LogInformation("Snapshotting {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SnapshotDeviceAsync(graphqlService, device, stoppingToken);
        }
    }

    private async Task SnapshotDeviceAsync(
        IOctopusGraphQLService graphqlService,
        HeatPumpDevice device, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(device.Euid))
        {
            _logger.LogWarning("Device {DeviceId} has no EUID, skipping snapshot", device.DeviceId);
            return;
        }

        var settings = await _store.GetSettingsAsync(device.OwnerId!, device.AccountNumber, stoppingToken);

        if (settings is null)
        {
            _logger.LogWarning("No settings found for owner {Owner} / account {Account}, skipping device {DeviceId}",
                device.OwnerId, device.AccountNumber, device.DeviceId);
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

            // Worker has no HttpContext — stamp tenant ownership explicitly from the device.
            snapshot.OwnerId = device.OwnerId;
            await _store.PutSnapshotAsync(snapshot, stoppingToken);
            _logger.LogInformation("Snapshotted device {DeviceId}: COP={COP}", device.DeviceId, snapshot.CoefficientOfPerformance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to snapshot device {DeviceId}", device.DeviceId);
        }
    }
}
