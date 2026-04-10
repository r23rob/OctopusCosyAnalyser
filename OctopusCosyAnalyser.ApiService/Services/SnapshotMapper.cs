using OctopusCosyAnalyser.ApiService.GraphQL;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;
using OctopusCosyAnalyser.Shared.Models;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Services;

public static class SnapshotMapper
{
    /// <summary>
    /// Maps the typed ZeroQL response from GetHeatPumpStatusAndConfigAsync into a HeatPumpSnapshot.
    /// </summary>
    public static HeatPumpSnapshot? MapFromStatusAndConfig(
        HeatPumpStatusAndConfigResponse response, string deviceId, string accountNumber)
    {
        var snapshot = new HeatPumpSnapshot
        {
            DeviceId = deviceId,
            AccountNumber = accountNumber,
            SnapshotTakenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        ExtractLivePerformance(response.TimeSeries, snapshot);
        ExtractLifetimePerformance(response.Lifetime, snapshot);
        ExtractControllerStatus(response.ControllerStatus, snapshot);
        ExtractControllerConfiguration(response.ControllerConfig, response.ControllerStatus, snapshot);
        ExtractSensorReadingsJson(response.ControllerStatus, snapshot);

        return snapshot;
    }

    private static void ExtractLivePerformance(TimeSeriesEntry?[]? timeSeries, HeatPumpSnapshot snapshot)
    {
        if (timeSeries is not { Length: > 0 })
            return;

        var live = timeSeries[^1];
        if (live is null)
            return;

        var energyIn = live.EnergyInput?.Value;
        var energyOut = live.EnergyOutput?.Value;

        if (energyIn.HasValue && energyOut.HasValue)
        {
            if (energyIn.Value > 0)
                snapshot.CoefficientOfPerformance = energyOut.Value / energyIn.Value;

            snapshot.HeatOutputKilowatt = energyOut.Value;
            snapshot.PowerInputKilowatt = energyIn.Value;
        }

        if (live.OutdoorTemperature?.Value is { } outdoorTemp)
            snapshot.OutdoorTemperatureCelsius = outdoorTemp;
    }

    private static void ExtractLifetimePerformance(LifetimePerformanceResponse? lifetime, HeatPumpSnapshot snapshot)
    {
        if (lifetime is null)
            return;

        if (lifetime.SeasonalCoefficientOfPerformance.HasValue)
            snapshot.SeasonalCoefficientOfPerformance = lifetime.SeasonalCoefficientOfPerformance.Value;

        if (lifetime.HeatOutput?.Value is { } lifetimeHeat)
            snapshot.LifetimeHeatOutputKwh = lifetimeHeat;

        if (lifetime.EnergyInput?.Value is { } lifetimeEnergy)
            snapshot.LifetimeEnergyInputKwh = lifetimeEnergy;
    }

    private static void ExtractControllerStatus(ControllerStatusResponse? status, HeatPumpSnapshot snapshot)
    {
        if (status is null)
            return;

        ExtractPrimaryZone(status, snapshot);
        ExtractPrimarySensor(status, snapshot);
        ExtractRoomSensorFromCosyPod(status, snapshot);
    }

    private static void ExtractPrimaryZone(ControllerStatusResponse status, HeatPumpSnapshot snapshot)
    {
        var zone = status.Zones?.FirstOrDefault(z => z is not null);
        var telemetry = zone?.Telemetry;
        if (telemetry is null)
            return;

        if (telemetry.SetpointInCelsius.HasValue)
            snapshot.PrimaryZoneSetpointCelsius = (decimal)telemetry.SetpointInCelsius.Value;

        if (telemetry.Mode.HasValue)
            snapshot.PrimaryZoneMode = telemetry.Mode.Value.ToString();

        if (telemetry.HeatDemand.HasValue)
            snapshot.PrimaryZoneHeatDemand = telemetry.HeatDemand.Value;
    }

    private static void ExtractPrimarySensor(ControllerStatusResponse status, HeatPumpSnapshot snapshot)
    {
        var sensor = status.Sensors?.FirstOrDefault(s => s is not null);
        if (sensor?.Telemetry?.TemperatureInCelsius is { } temp)
            snapshot.PrimarySensorTemperatureCelsius = (decimal)temp;
    }

    private static void ExtractRoomSensorFromCosyPod(ControllerStatusResponse status, HeatPumpSnapshot snapshot)
    {
        if (status.Sensors is null)
            return;

        foreach (var sensor in status.Sensors)
        {
            if (sensor?.Telemetry?.HumidityPercentage is { } humidity)
            {
                snapshot.RoomHumidityPercentage = humidity;
                if (sensor.Telemetry.TemperatureInCelsius is { } roomTemp)
                    snapshot.RoomTemperatureCelsius = (decimal)roomTemp;
                snapshot.RoomSensorCode = sensor.Code;
                break;
            }
        }
    }

    private static void ExtractControllerConfiguration(
        ControllerConfigResponse? config, ControllerStatusResponse? status, HeatPumpSnapshot snapshot)
    {
        if (config is null)
            return;

        if (config.Controller is { } controller)
        {
            snapshot.ControllerConnected = controller.Connected;

            if (controller.State is { Length: > 0 } states)
            {
                var stateStrings = states
                    .Where(s => s.HasValue)
                    .Select(s => s!.Value.ToString())
                    .ToList();
                if (stateStrings.Count > 0)
                    snapshot.ControllerState = string.Join(",", stateStrings);
            }
        }

        if (config.HeatPump is { } heatPump)
        {
            ExtractWeatherCompensation(heatPump, snapshot);
            ExtractFlowTemperature(heatPump, snapshot);
        }

        ExtractZones(config, status, snapshot);
    }

    private static void ExtractWeatherCompensation(HeatPumpConfigResponse heatPump, HeatPumpSnapshot snapshot)
    {
        if (heatPump.WeatherCompensation is not { } wc)
            return;

        if (wc.Enabled)
        {
            snapshot.FlowTempMode = FlowTempMode.WeatherCompensation;
            snapshot.WeatherCompensationMinCelsius = wc.CurrentRange?.Minimum?.Value;
            snapshot.WeatherCompensationMaxCelsius = wc.CurrentRange?.Maximum?.Value;
            snapshot.HeatingFlowTemperatureCelsius = null;
        }
        else
        {
            snapshot.FlowTempMode = FlowTempMode.FixedFlow;
            snapshot.WeatherCompensationMinCelsius = null;
            snapshot.WeatherCompensationMaxCelsius = null;
        }
    }

    private static void ExtractFlowTemperature(HeatPumpConfigResponse heatPump, HeatPumpSnapshot snapshot)
    {
        if (heatPump.HeatingFlowTemperature is not { } flowTemp)
            return;

        snapshot.HeatingFlowTempAllowableMinCelsius = flowTemp.AllowableRange?.Minimum?.Value;
        snapshot.HeatingFlowTempAllowableMaxCelsius = flowTemp.AllowableRange?.Maximum?.Value;

        if (snapshot.FlowTempMode == FlowTempMode.FixedFlow)
            snapshot.HeatingFlowTemperatureCelsius = flowTemp.CurrentTemperature?.Value;
    }

    private static void ExtractZones(
        ControllerConfigResponse config, ControllerStatusResponse? status, HeatPumpSnapshot snapshot)
    {
        if (config.Zones is null || status?.Zones is null)
            return;

        // Find heating and hot water zone codes from configuration
        Zone? heatingZoneCode = null;
        Zone? hotWaterZoneCode = null;

        foreach (var zi in config.Zones)
        {
            if (zi?.Configuration is not { } zc)
                continue;

            if (zc.ZoneType == ZoneType.Heat && heatingZoneCode is null)
                heatingZoneCode = zc.Code;
            else if (zc.ZoneType == ZoneType.Water && hotWaterZoneCode is null)
                hotWaterZoneCode = zc.Code;
        }

        // Match status zones to configured zones
        foreach (var sz in status.Zones)
        {
            if (sz?.Zone is null || sz.Telemetry is null)
                continue;

            var telemetry = sz.Telemetry;

            if (sz.Zone == heatingZoneCode)
            {
                if (telemetry.SetpointInCelsius.HasValue)
                    snapshot.HeatingZoneSetpointCelsius = (decimal)telemetry.SetpointInCelsius.Value;
                if (telemetry.Mode.HasValue)
                    snapshot.HeatingZoneMode = telemetry.Mode.Value.ToString();
                if (telemetry.HeatDemand.HasValue)
                    snapshot.HeatingZoneHeatDemand = telemetry.HeatDemand.Value;
            }
            else if (sz.Zone == hotWaterZoneCode)
            {
                if (telemetry.SetpointInCelsius.HasValue)
                    snapshot.HotWaterZoneSetpointCelsius = (decimal)telemetry.SetpointInCelsius.Value;
                if (telemetry.Mode.HasValue)
                    snapshot.HotWaterZoneMode = telemetry.Mode.Value.ToString();
                if (telemetry.HeatDemand.HasValue)
                    snapshot.HotWaterZoneHeatDemand = telemetry.HeatDemand.Value;
            }
        }

        // Fallback: if status zone matching found nothing for hot water,
        // try extracting directly from config zone data
        if (hotWaterZoneCode is not null && !snapshot.HotWaterZoneHeatDemand.HasValue)
        {
            foreach (var zi in config.Zones)
            {
                if (zi?.Configuration is not { } zc || zc.ZoneType != ZoneType.Water)
                    continue;

                snapshot.HotWaterZoneHeatDemand = zc.HeatDemand ?? zc.CallForHeat;

                if (!snapshot.HotWaterZoneSetpointCelsius.HasValue && zc.CurrentOperation?.SetpointInCelsius is { } opSetpoint)
                    snapshot.HotWaterZoneSetpointCelsius = (decimal)opSetpoint;

                if (string.IsNullOrEmpty(snapshot.HotWaterZoneMode) && zc.CurrentOperation?.Mode is { } opMode)
                    snapshot.HotWaterZoneMode = opMode.ToString();

                break;
            }
        }
    }

    private static void ExtractSensorReadingsJson(ControllerStatusResponse? status, HeatPumpSnapshot snapshot)
    {
        if (status?.Sensors is not { Length: > 0 } sensors)
            return;

        var sensorList = new List<object>();
        foreach (var sensor in sensors)
        {
            if (sensor is null) continue;

            var entry = new Dictionary<string, object?>();
            entry["code"] = sensor.Code;

            if (sensor.Connectivity?.Online is { } online)
                entry["online"] = online;

            if (sensor.Telemetry is { } tel)
            {
                if (tel.TemperatureInCelsius is { } tempC)
                    entry["tempC"] = (decimal)tempC;
                if (tel.HumidityPercentage is { } humidity)
                    entry["humidity"] = (decimal)humidity;
            }

            sensorList.Add(entry);
        }

        if (sensorList.Count > 0)
            snapshot.SensorReadingsJson = JsonSerializer.Serialize(sensorList);
    }
}
