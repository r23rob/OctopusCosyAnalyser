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
                        if (zoneTelemetry.TryGetProperty("setpointInCelsius", out var setpoint) && setpoint.ValueKind == JsonValueKind.Number && setpoint.TryGetDecimal(out var setpointDec))
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
                    if (sensor.TryGetProperty("telemetry", out var sensorTelemetry) && sensorTelemetry.TryGetProperty("temperatureInCelsius", out var temp) && temp.ValueKind == JsonValueKind.Number && temp.TryGetDecimal(out var tempDec))
                        snapshot.PrimarySensorTemperatureCelsius = tempDec;
                }

                // Find first Cosy Pod sensor (has humidity) for room temperature/humidity
                if (status.TryGetProperty("sensors", out var allSensors))
                {
                    foreach (var s in allSensors.EnumerateArray())
                    {
                        if (s.TryGetProperty("telemetry", out var sTelemetry)
                            && sTelemetry.TryGetProperty("humidityPercentage", out var humidity)
                            && humidity.ValueKind == JsonValueKind.Number
                            && humidity.TryGetDecimal(out var humidityDec))
                        {
                            snapshot.RoomHumidityPercentage = humidityDec;
                            if (sTelemetry.TryGetProperty("temperatureInCelsius", out var roomTemp) && roomTemp.ValueKind == JsonValueKind.Number && roomTemp.TryGetDecimal(out var roomTempDec))
                                snapshot.RoomTemperatureCelsius = roomTempDec;
                            if (s.TryGetProperty("code", out var sCode))
                                snapshot.RoomSensorCode = sCode.GetString();
                            break;
                        }
                    }
                }
            }

            // Extract controller configuration
            if (root.TryGetProperty("octoHeatPumpControllerConfiguration", out var config))
            {
                if (config.TryGetProperty("controller", out var controller))
                {
                    if (controller.TryGetProperty("connected", out var connected))
                        snapshot.ControllerConnected = connected.ValueKind == JsonValueKind.True;

                    // Controller state (e.g. "HEATING", "IDLE")
                    if (controller.TryGetProperty("state", out var stateArray) && stateArray.ValueKind == JsonValueKind.Array)
                    {
                        var states = new List<string>();
                        foreach (var s in stateArray.EnumerateArray())
                        {
                            var str = s.GetString();
                            if (!string.IsNullOrWhiteSpace(str))
                                states.Add(str);
                        }
                        if (states.Count > 0)
                            snapshot.ControllerState = string.Join(",", states);
                    }
                }

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

                    // Current heating flow temperature and allowable range
                    if (heatPump.TryGetProperty("heatingFlowTemperature", out var flowTemp))
                    {
                        if (flowTemp.TryGetProperty("currentTemperature", out var currentFlow)
                            && currentFlow.TryGetProperty("value", out var flowVal)
                            && decimal.TryParse(flowVal.GetString() ?? "", out var flowDec))
                            snapshot.HeatingFlowTemperatureCelsius = flowDec;

                        if (flowTemp.TryGetProperty("allowableRange", out var flowRange))
                        {
                            if (flowRange.TryGetProperty("minimum", out var flowMin) && flowMin.TryGetProperty("value", out var flowMinVal)
                                && decimal.TryParse(flowMinVal.GetString() ?? "", out var flowMinDec))
                                snapshot.HeatingFlowTempAllowableMinCelsius = flowMinDec;

                            if (flowRange.TryGetProperty("maximum", out var flowMax) && flowMax.TryGetProperty("value", out var flowMaxVal)
                                && decimal.TryParse(flowMaxVal.GetString() ?? "", out var flowMaxDec))
                                snapshot.HeatingFlowTempAllowableMaxCelsius = flowMaxDec;
                        }
                    }
                }

                // Find zones from config, look them up in status zones
                if (root.TryGetProperty("octoHeatPumpControllerStatus", out var statusForZone)
                    && config.TryGetProperty("zones", out var configZones)
                    && statusForZone.TryGetProperty("zones", out var statusZones)
                    && configZones.ValueKind == JsonValueKind.Array
                    && statusZones.ValueKind == JsonValueKind.Array)
                {
                    string? heatingZoneCode = null;
                    string? hotWaterZoneCode = null;

                    foreach (var cz in configZones.EnumerateArray())
                    {
                        if (cz.TryGetProperty("configuration", out var czConfig)
                            && czConfig.TryGetProperty("zoneType", out var zt)
                            && czConfig.TryGetProperty("code", out var czCode))
                        {
                            var zoneType = zt.GetString();
                            if (zoneType == "HEAT" && heatingZoneCode is null)
                                heatingZoneCode = czCode.GetString();
                            else if (zoneType == "HOT_WATER" && hotWaterZoneCode is null)
                                hotWaterZoneCode = czCode.GetString();
                        }
                    }

                    foreach (var sz in statusZones.EnumerateArray())
                    {
                        if (!sz.TryGetProperty("zone", out var szZone) || !sz.TryGetProperty("telemetry", out var szTelemetry))
                            continue;

                        var zoneCode = szZone.GetString();

                        if (zoneCode == heatingZoneCode)
                        {
                            if (szTelemetry.TryGetProperty("setpointInCelsius", out var hzSetpoint) && hzSetpoint.ValueKind == JsonValueKind.Number && hzSetpoint.TryGetDecimal(out var hzSetpointDec))
                                snapshot.HeatingZoneSetpointCelsius = hzSetpointDec;
                            if (szTelemetry.TryGetProperty("mode", out var hzMode))
                                snapshot.HeatingZoneMode = hzMode.GetString();
                            if (szTelemetry.TryGetProperty("heatDemand", out var hzHeatDemand))
                                snapshot.HeatingZoneHeatDemand = hzHeatDemand.ValueKind == JsonValueKind.True;
                        }
                        else if (zoneCode == hotWaterZoneCode)
                        {
                            if (szTelemetry.TryGetProperty("setpointInCelsius", out var hwSetpoint) && hwSetpoint.ValueKind == JsonValueKind.Number && hwSetpoint.TryGetDecimal(out var hwSetpointDec))
                                snapshot.HotWaterZoneSetpointCelsius = hwSetpointDec;
                            if (szTelemetry.TryGetProperty("mode", out var hwMode))
                                snapshot.HotWaterZoneMode = hwMode.GetString();
                            if (szTelemetry.TryGetProperty("heatDemand", out var hwHeatDemand))
                                snapshot.HotWaterZoneHeatDemand = hwHeatDemand.ValueKind == JsonValueKind.True;
                        }
                    }
                }
            }

            // Serialize all sensor readings to JSONB
            if (root.TryGetProperty("octoHeatPumpControllerStatus", out var statusForSensors)
                && statusForSensors.TryGetProperty("sensors", out var allSensorsForJson)
                && allSensorsForJson.ValueKind == JsonValueKind.Array)
            {
                var sensorList = new List<object>();
                foreach (var sensor in allSensorsForJson.EnumerateArray())
                {
                    var entry = new Dictionary<string, object?>();
                    if (sensor.TryGetProperty("code", out var code))
                        entry["code"] = code.GetString();
                    if (sensor.TryGetProperty("connectivity", out var conn) && conn.TryGetProperty("online", out var online))
                        entry["online"] = online.ValueKind == JsonValueKind.True;
                    if (sensor.TryGetProperty("telemetry", out var tel))
                    {
                        if (tel.TryGetProperty("temperatureInCelsius", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetDecimal(out var tempC))
                            entry["tempC"] = tempC;
                        if (tel.TryGetProperty("humidityPercentage", out var h) && h.ValueKind == JsonValueKind.Number && h.TryGetDecimal(out var humidity))
                            entry["humidity"] = humidity;
                    }
                    sensorList.Add(entry);
                }
                if (sensorList.Count > 0)
                    snapshot.SensorReadingsJson = JsonSerializer.Serialize(sensorList);
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

