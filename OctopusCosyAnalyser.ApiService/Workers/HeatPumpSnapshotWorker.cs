using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class HeatPumpSnapshotWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeatPumpSnapshotWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(15);

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
        var client = scope.ServiceProvider.GetRequiredService<OctopusEnergyClient>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive)
            .ToListAsync(stoppingToken);

        _logger.LogInformation("Snapshotting {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SnapshotDeviceAsync(db, client, device, stoppingToken);
        }

        await db.SaveChangesAsync(stoppingToken);
    }

    private async Task SnapshotDeviceAsync(CosyDbContext db, OctopusEnergyClient client, HeatPumpDevice device, CancellationToken stoppingToken)
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
            var data = await client.GetHeatPumpStatusAndConfigAsync(settings.ApiKey, device.AccountNumber, device.Euid);
            var root = data.RootElement.GetProperty("data");

            var snapshot = new HeatPumpSnapshot
            {
                DeviceId = device.DeviceId,
                AccountNumber = device.AccountNumber,
                SnapshotTakenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Extract live performance
            if (root.TryGetProperty("octoHeatPumpLivePerformance", out var live))
            {
                if (decimal.TryParse(live.GetProperty("coefficientOfPerformance").GetString() ?? "", out var cop))
                    snapshot.CoefficientOfPerformance = cop;

                if (live.TryGetProperty("outdoorTemperature", out var outdoorTemp) && outdoorTemp.TryGetProperty("value", out var tempVal))
                    if (decimal.TryParse(tempVal.GetString() ?? "", out var tempDec))
                        snapshot.OutdoorTemperatureCelsius = tempDec;

                if (live.TryGetProperty("heatOutput", out var heatOut) && heatOut.TryGetProperty("value", out var heatVal))
                    if (decimal.TryParse(heatVal.GetString() ?? "", out var heatDec))
                        snapshot.HeatOutputKilowatt = heatDec;

                if (live.TryGetProperty("powerInput", out var powerIn) && powerIn.TryGetProperty("value", out var powerVal))
                    if (decimal.TryParse(powerVal.GetString() ?? "", out var powerDec))
                        snapshot.PowerInputKilowatt = powerDec;
            }

            // Extract lifetime performance
            if (root.TryGetProperty("octoHeatPumpLifetimePerformance", out var lifetime))
            {
                if (decimal.TryParse(lifetime.GetProperty("seasonalCoefficientOfPerformance").GetString() ?? "", out var scop))
                    snapshot.SeasonalCoefficientOfPerformance = scop;

                if (lifetime.TryGetProperty("heatOutput", out var lifetimeHeat) && lifetimeHeat.TryGetProperty("value", out var lifetimeHeatVal))
                    if (decimal.TryParse(lifetimeHeatVal.GetString() ?? "", out var lifetimeHeatDec))
                        snapshot.LifetimeHeatOutputKwh = lifetimeHeatDec;

                if (lifetime.TryGetProperty("energyInput", out var lifetimeEnergy) && lifetimeEnergy.TryGetProperty("value", out var lifetimeEnergyVal))
                    if (decimal.TryParse(lifetimeEnergyVal.GetString() ?? "", out var lifetimeEnergyDec))
                        snapshot.LifetimeEnergyInputKwh = lifetimeEnergyDec;
            }

            // Extract controller status
            if (root.TryGetProperty("octoHeatPumpControllerStatus", out var status))
            {
                // Get primary zone telemetry
                if (status.TryGetProperty("zones", out var zones) && zones.GetArrayLength() > 0)
                {
                    var zone = zones[0];
                    if (zone.TryGetProperty("telemetry", out var zoneTelemetry))
                    {
                        if (zoneTelemetry.TryGetProperty("setpointInCelsius", out var setpoint) && setpoint.TryGetDecimal(out var setpointDec))
                            snapshot.PrimaryZoneSetpointCelsius = setpointDec;

                        if (zoneTelemetry.TryGetProperty("mode", out var mode))
                            snapshot.PrimaryZoneMode = mode.GetString();

                        if (zoneTelemetry.TryGetProperty("heatDemand", out var heatDemand))
                            snapshot.PrimaryZoneHeatDemand = heatDemand.ValueKind == JsonValueKind.True;
                    }
                }

                // Get primary sensor temperature
                if (status.TryGetProperty("sensors", out var sensors) && sensors.GetArrayLength() > 0)
                {
                    var sensor = sensors[0];
                    if (sensor.TryGetProperty("telemetry", out var sensorTelemetry) && sensorTelemetry.TryGetProperty("temperatureInCelsius", out var temp) && temp.TryGetDecimal(out var tempDec))
                        snapshot.PrimarySensorTemperatureCelsius = tempDec;
                }
            }

            // Extract controller configuration
            if (root.TryGetProperty("octoHeatPumpControllerConfiguration", out var config))
            {
                if (config.TryGetProperty("controller", out var controller) && controller.TryGetProperty("connected", out var connected))
                    snapshot.ControllerConnected = connected.ValueKind == JsonValueKind.True;

                // Extract weather compensation & flow temperature from heatPump config
                if (config.TryGetProperty("heatPump", out var heatPump))
                {
                    // Weather compensation enabled + current range (min/max)
                    if (heatPump.TryGetProperty("weatherCompensation", out var weatherComp))
                    {
                        if (weatherComp.TryGetProperty("enabled", out var wcEnabled))
                            snapshot.WeatherCompensationEnabled = wcEnabled.ValueKind == JsonValueKind.True;

                        if (weatherComp.TryGetProperty("currentRange", out var wcRange))
                        {
                            if (wcRange.TryGetProperty("minimum", out var wcMin) && wcMin.TryGetProperty("value", out var wcMinVal))
                                if (decimal.TryParse(wcMinVal.GetString() ?? "", out var wcMinDec))
                                    snapshot.WeatherCompensationMinCelsius = wcMinDec;

                            if (wcRange.TryGetProperty("maximum", out var wcMax) && wcMax.TryGetProperty("value", out var wcMaxVal))
                                if (decimal.TryParse(wcMaxVal.GetString() ?? "", out var wcMaxDec))
                                    snapshot.WeatherCompensationMaxCelsius = wcMaxDec;
                        }
                    }

                    // Current heating flow temperature
                    if (heatPump.TryGetProperty("heatingFlowTemperature", out var flowTemp)
                        && flowTemp.TryGetProperty("currentTemperature", out var currentFlow)
                        && currentFlow.TryGetProperty("value", out var flowVal))
                    {
                        if (decimal.TryParse(flowVal.GetString() ?? "", out var flowDec))
                            snapshot.HeatingFlowTemperatureCelsius = flowDec;
                    }
                }
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

