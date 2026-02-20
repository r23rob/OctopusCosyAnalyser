using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Features.HeatPumpSnapshots;

/// <summary>
/// Use-case: take a live snapshot from every active heat pump device and persist it.
/// Invoked by the scheduler worker; all snapshot logic lives here.
/// </summary>
public class TakeHeatPumpSnapshots(
    IHeatPumpSnapshotRepository repo,
    IHeatPumpProvider provider,
    ILogger<TakeHeatPumpSnapshots> logger)
{
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var devices = await repo.GetActiveDevicesAsync(ct);
        logger.LogInformation("Snapshotting {Count} active devices", devices.Count);

        foreach (var device in devices)
            await SnapshotDeviceAsync(device, ct);

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
            logger.LogWarning("No settings found for account {Account}, skipping device {DeviceId}",
                device.AccountNumber, device.DeviceId);
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

            if (lifetime.TryGetProperty("heatOutput", out var lhHeat) && lhHeat.TryGetProperty("value", out var lhHeatVal))
                if (decimal.TryParse(lhHeatVal.GetString() ?? "", out var lhHeatDec))
                    snapshot.LifetimeHeatOutputKwh = lhHeatDec;

            if (lifetime.TryGetProperty("energyInput", out var lhEnergy) && lhEnergy.TryGetProperty("value", out var lhEnergyVal))
                if (decimal.TryParse(lhEnergyVal.GetString() ?? "", out var lhEnergyDec))
                    snapshot.LifetimeEnergyInputKwh = lhEnergyDec;
        }

        if (root.TryGetProperty("octoHeatPumpControllerStatus", out var status))
        {
            if (status.TryGetProperty("zones", out var zones) && zones.GetArrayLength() > 0)
            {
                var zone = zones[0];
                if (zone.TryGetProperty("telemetry", out var zt))
                {
                    if (zt.TryGetProperty("setpointInCelsius", out var sp) && sp.TryGetDecimal(out var spDec))
                        snapshot.PrimaryZoneSetpointCelsius = spDec;
                    if (zt.TryGetProperty("mode", out var mode))
                        snapshot.PrimaryZoneMode = mode.GetString();
                    if (zt.TryGetProperty("heatDemand", out var hd))
                        snapshot.PrimaryZoneHeatDemand = hd.ValueKind == JsonValueKind.True;
                }
            }

            if (status.TryGetProperty("sensors", out var sensors) && sensors.GetArrayLength() > 0)
            {
                var sensor = sensors[0];
                if (sensor.TryGetProperty("telemetry", out var st)
                    && st.TryGetProperty("temperatureInCelsius", out var t)
                    && t.TryGetDecimal(out var tDec))
                    snapshot.PrimarySensorTemperatureCelsius = tDec;
            }

            // Find first sensor with humidity (Cosy Pod) for room readings
            if (status.TryGetProperty("sensors", out var allSensors))
            {
                foreach (var s in allSensors.EnumerateArray())
                {
                    if (s.TryGetProperty("telemetry", out var st)
                        && st.TryGetProperty("humidityPercentage", out var hum)
                        && hum.TryGetDecimal(out var humDec))
                    {
                        snapshot.RoomHumidityPercentage = humDec;
                        if (st.TryGetProperty("temperatureInCelsius", out var rt) && rt.TryGetDecimal(out var rtDec))
                            snapshot.RoomTemperatureCelsius = rtDec;
                        if (s.TryGetProperty("code", out var sc))
                            snapshot.RoomSensorCode = sc.GetString();
                        break;
                    }
                }
            }
        }

        if (root.TryGetProperty("octoHeatPumpControllerConfiguration", out var config))
        {
            if (config.TryGetProperty("controller", out var ctrl) && ctrl.TryGetProperty("connected", out var conn))
                snapshot.ControllerConnected = conn.ValueKind == JsonValueKind.True;

            if (config.TryGetProperty("heatPump", out var hp))
            {
                if (hp.TryGetProperty("weatherCompensation", out var wc))
                {
                    if (wc.TryGetProperty("enabled", out var wcOn))
                        snapshot.WeatherCompensationEnabled = wcOn.ValueKind == JsonValueKind.True;

                    if (wc.TryGetProperty("currentRange", out var wcRange))
                    {
                        if (wcRange.TryGetProperty("minimum", out var wcMin) && wcMin.TryGetProperty("value", out var wcMinVal)
                            && decimal.TryParse(wcMinVal.GetString() ?? "", out var wcMinDec))
                            snapshot.WeatherCompensationMinCelsius = wcMinDec;

                        if (wcRange.TryGetProperty("maximum", out var wcMax) && wcMax.TryGetProperty("value", out var wcMaxVal)
                            && decimal.TryParse(wcMaxVal.GetString() ?? "", out var wcMaxDec))
                            snapshot.WeatherCompensationMaxCelsius = wcMaxDec;
                    }
                }

                if (hp.TryGetProperty("heatingFlowTemperature", out var ft)
                    && ft.TryGetProperty("currentTemperature", out var cft)
                    && cft.TryGetProperty("value", out var cftVal)
                    && decimal.TryParse(cftVal.GetString() ?? "", out var cftDec))
                    snapshot.HeatingFlowTemperatureCelsius = cftDec;
            }

            // Match the first HEAT zone from config to status zones for setpoint/mode
            if (root.TryGetProperty("octoHeatPumpControllerStatus", out var statusForZone)
                && config.TryGetProperty("zones", out var configZones)
                && statusForZone.TryGetProperty("zones", out var statusZones))
            {
                string? heatingZoneCode = null;
                foreach (var cz in configZones.EnumerateArray())
                {
                    if (cz.TryGetProperty("configuration", out var czCfg)
                        && czCfg.TryGetProperty("zoneType", out var zt)
                        && zt.GetString() == "HEAT"
                        && czCfg.TryGetProperty("code", out var czCode))
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
                            && sz.TryGetProperty("telemetry", out var szT))
                        {
                            if (szT.TryGetProperty("setpointInCelsius", out var hzSp) && hzSp.TryGetDecimal(out var hzSpDec))
                                snapshot.HeatingZoneSetpointCelsius = hzSpDec;
                            if (szT.TryGetProperty("mode", out var hzMode))
                                snapshot.HeatingZoneMode = hzMode.GetString();
                            if (szT.TryGetProperty("heatDemand", out var hzHd))
                                snapshot.HeatingZoneHeatDemand = hzHd.ValueKind == JsonValueKind.True;
                            break;
                        }
                    }
                }
            }
        }

        return snapshot;
    }
}
