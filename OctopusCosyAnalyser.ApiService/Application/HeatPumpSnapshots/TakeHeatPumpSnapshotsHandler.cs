using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Application.HeatPumpSnapshots;

/// <summary>
/// Command: take a live snapshot for every active heat pump device and persist it.
/// The worker schedules this; the Application layer owns the logic.
/// </summary>
public record TakeHeatPumpSnapshotsCommand;

public class TakeHeatPumpSnapshotsHandler(
    IHeatPumpSnapshotRepository repo,
    IHeatPumpProvider provider,
    ILogger<TakeHeatPumpSnapshotsHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct = default)
    {
        var devices = await repo.GetActiveDevicesAsync(ct);
        logger.LogInformation("Snapshotting {Count} active devices", devices.Count);

        foreach (var device in devices)
        {
            await SnapshotDeviceAsync(device, ct);
        }

        await repo.SaveChangesAsync(ct);
    }

    private async Task SnapshotDeviceAsync(HeatPumpDevice device, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(device.Euid))
        {
            logger.LogWarning("Device {DeviceId} has no EUID, skipping snapshot", device.DeviceId);
            return;
        }

        var settings = await repo.GetSettingsForAccountAsync(device.AccountNumber, ct);
        if (settings is null)
        {
            logger.LogWarning("No settings found for account {Account}, skipping device {DeviceId}", device.AccountNumber, device.DeviceId);
            return;
        }

        try
        {
            var data = await provider.GetHeatPumpStatusAndConfigAsync(settings.ApiKey, device.AccountNumber, device.Euid);
            var snapshot = BuildSnapshot(device, data.RootElement.GetProperty("data"));
            await repo.AddSnapshotAsync(snapshot, ct);
            logger.LogInformation("Snapshotted device {DeviceId}: COP={COP}", device.DeviceId, snapshot.CoefficientOfPerformance);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to snapshot device {DeviceId}", device.DeviceId);
        }
    }

    private static HeatPumpSnapshot BuildSnapshot(HeatPumpDevice device, JsonElement root)
    {
        var snapshot = new HeatPumpSnapshot
        {
            DeviceId = device.DeviceId,
            AccountNumber = device.AccountNumber,
            SnapshotTakenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

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

        if (root.TryGetProperty("octoHeatPumpControllerStatus", out var status))
        {
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

            if (status.TryGetProperty("sensors", out var sensors) && sensors.GetArrayLength() > 0)
            {
                var sensor = sensors[0];
                if (sensor.TryGetProperty("telemetry", out var sensorTelemetry) && sensorTelemetry.TryGetProperty("temperatureInCelsius", out var temp) && temp.TryGetDecimal(out var tempDec))
                    snapshot.PrimarySensorTemperatureCelsius = tempDec;
            }

            if (status.TryGetProperty("sensors", out var allSensors))
            {
                foreach (var s in allSensors.EnumerateArray())
                {
                    if (s.TryGetProperty("telemetry", out var sTelemetry)
                        && sTelemetry.TryGetProperty("humidityPercentage", out var humidity)
                        && humidity.TryGetDecimal(out var humidityDec))
                    {
                        snapshot.RoomHumidityPercentage = humidityDec;
                        if (sTelemetry.TryGetProperty("temperatureInCelsius", out var roomTemp) && roomTemp.TryGetDecimal(out var roomTempDec))
                            snapshot.RoomTemperatureCelsius = roomTempDec;
                        if (s.TryGetProperty("code", out var sCode))
                            snapshot.RoomSensorCode = sCode.GetString();
                        break;
                    }
                }
            }
        }

        if (root.TryGetProperty("octoHeatPumpControllerConfiguration", out var config))
        {
            if (config.TryGetProperty("controller", out var controller) && controller.TryGetProperty("connected", out var connected))
                snapshot.ControllerConnected = connected.ValueKind == JsonValueKind.True;

            if (config.TryGetProperty("heatPump", out var heatPump))
            {
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

                if (heatPump.TryGetProperty("heatingFlowTemperature", out var flowTemp)
                    && flowTemp.TryGetProperty("currentTemperature", out var currentFlow)
                    && currentFlow.TryGetProperty("value", out var flowVal))
                {
                    if (decimal.TryParse(flowVal.GetString() ?? "", out var flowDec))
                        snapshot.HeatingFlowTemperatureCelsius = flowDec;
                }
            }

            if (root.TryGetProperty("octoHeatPumpControllerStatus", out var statusForZone)
                && config.TryGetProperty("zones", out var configZones)
                && statusForZone.TryGetProperty("zones", out var statusZones))
            {
                string? heatingZoneCode = null;
                foreach (var cz in configZones.EnumerateArray())
                {
                    if (cz.TryGetProperty("configuration", out var czConfig)
                        && czConfig.TryGetProperty("zoneType", out var zt)
                        && zt.GetString() == "HEAT"
                        && czConfig.TryGetProperty("code", out var czCode))
                    {
                        heatingZoneCode = czCode.GetString();
                        break;
                    }
                }

                if (heatingZoneCode is not null)
                {
                    foreach (var sz in statusZones.EnumerateArray())
                    {
                        if (sz.TryGetProperty("zone", out var szZone) && szZone.GetString() == heatingZoneCode
                            && sz.TryGetProperty("telemetry", out var szTelemetry))
                        {
                            if (szTelemetry.TryGetProperty("setpointInCelsius", out var hzSetpoint) && hzSetpoint.TryGetDecimal(out var hzSetpointDec))
                                snapshot.HeatingZoneSetpointCelsius = hzSetpointDec;
                            if (szTelemetry.TryGetProperty("mode", out var hzMode))
                                snapshot.HeatingZoneMode = hzMode.GetString();
                            if (szTelemetry.TryGetProperty("heatDemand", out var hzHeatDemand))
                                snapshot.HeatingZoneHeatDemand = hzHeatDemand.ValueKind == JsonValueKind.True;
                            break;
                        }
                    }
                }
            }
        }

        return snapshot;
    }
}
