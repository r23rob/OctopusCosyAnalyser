using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Services;

public static class SnapshotMapper
{
    /// <summary>
    /// Maps the JSON response from GetHeatPumpStatusAndConfigAsync into a HeatPumpSnapshot.
    /// Returns null if the JSON lacks a "data" root property.
    /// </summary>
    public static HeatPumpSnapshot? MapFromStatusAndConfig(JsonDocument doc, string deviceId, string accountNumber)
    {
        if (!doc.RootElement.TryGetProperty("data", out var root))
            return null;

        var snapshot = new HeatPumpSnapshot
        {
            DeviceId = deviceId,
            AccountNumber = accountNumber,
            SnapshotTakenAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        ExtractLivePerformance(root, snapshot);
        ExtractLifetimePerformance(root, snapshot);
        ExtractControllerStatus(root, snapshot);
        ExtractControllerConfiguration(root, snapshot);
        ExtractSensorReadingsJson(root, snapshot);

        return snapshot;
    }

    private static void ExtractLivePerformance(JsonElement root, HeatPumpSnapshot snapshot)
    {
        if (!root.TryGetProperty("heatPumpTimeSeriesPerformance", out var liveArray)
            || liveArray.ValueKind != JsonValueKind.Array
            || liveArray.GetArrayLength() == 0)
            return;

        var live = liveArray.EnumerateArray().Last();

        if (TryGetDecimalValue(live, "energyInput", out var energyIn)
            && TryGetDecimalValue(live, "energyOutput", out var energyOut))
        {
            if (energyIn > 0)
                snapshot.CoefficientOfPerformance = energyOut / energyIn;

            snapshot.HeatOutputKilowatt = energyOut;
            snapshot.PowerInputKilowatt = energyIn;
        }

        if (TryGetDecimalValue(live, "outdoorTemperature", out var outdoorTemp))
            snapshot.OutdoorTemperatureCelsius = outdoorTemp;
    }

    private static void ExtractLifetimePerformance(JsonElement root, HeatPumpSnapshot snapshot)
    {
        if (!root.TryGetProperty("heatPumpLifetimePerformance", out var lifetime))
            return;

        if (decimal.TryParse(lifetime.GetProperty("seasonalCoefficientOfPerformance").GetString() ?? "", out var scop))
            snapshot.SeasonalCoefficientOfPerformance = scop;

        if (TryGetDecimalValue(lifetime, "heatOutput", out var lifetimeHeat))
            snapshot.LifetimeHeatOutputKwh = lifetimeHeat;

        if (TryGetDecimalValue(lifetime, "energyInput", out var lifetimeEnergy))
            snapshot.LifetimeEnergyInputKwh = lifetimeEnergy;
    }

    private static void ExtractControllerStatus(JsonElement root, HeatPumpSnapshot snapshot)
    {
        if (!root.TryGetProperty("heatPumpControllerStatus", out var status))
            return;

        ExtractPrimaryZone(status, snapshot);
        ExtractPrimarySensor(status, snapshot);
        ExtractRoomSensorFromCosyPod(status, snapshot);
    }

    private static void ExtractPrimaryZone(JsonElement status, HeatPumpSnapshot snapshot)
    {
        if (!status.TryGetProperty("zones", out var zones) || zones.GetArrayLength() == 0)
            return;

        var zone = zones[0];
        if (!zone.TryGetProperty("telemetry", out var telemetry))
            return;

        if (telemetry.TryGetProperty("setpointInCelsius", out var setpoint)
            && setpoint.ValueKind == JsonValueKind.Number
            && setpoint.TryGetDecimal(out var setpointDec))
            snapshot.PrimaryZoneSetpointCelsius = setpointDec;

        if (telemetry.TryGetProperty("mode", out var mode))
            snapshot.PrimaryZoneMode = mode.GetString();

        if (telemetry.TryGetProperty("heatDemand", out var heatDemand))
            snapshot.PrimaryZoneHeatDemand = heatDemand.ValueKind == JsonValueKind.True;
    }

    private static void ExtractPrimarySensor(JsonElement status, HeatPumpSnapshot snapshot)
    {
        if (!status.TryGetProperty("sensors", out var sensors) || sensors.GetArrayLength() == 0)
            return;

        var sensor = sensors[0];
        if (sensor.TryGetProperty("telemetry", out var telemetry)
            && telemetry.TryGetProperty("temperatureInCelsius", out var temp)
            && temp.ValueKind == JsonValueKind.Number
            && temp.TryGetDecimal(out var tempDec))
            snapshot.PrimarySensorTemperatureCelsius = tempDec;
    }

    private static void ExtractRoomSensorFromCosyPod(JsonElement status, HeatPumpSnapshot snapshot)
    {
        if (!status.TryGetProperty("sensors", out var allSensors))
            return;

        foreach (var s in allSensors.EnumerateArray())
        {
            if (s.TryGetProperty("telemetry", out var telemetry)
                && telemetry.TryGetProperty("humidityPercentage", out var humidity)
                && humidity.ValueKind == JsonValueKind.Number
                && humidity.TryGetDecimal(out var humidityDec))
            {
                snapshot.RoomHumidityPercentage = humidityDec;
                if (telemetry.TryGetProperty("temperatureInCelsius", out var roomTemp)
                    && roomTemp.ValueKind == JsonValueKind.Number
                    && roomTemp.TryGetDecimal(out var roomTempDec))
                    snapshot.RoomTemperatureCelsius = roomTempDec;
                if (s.TryGetProperty("code", out var sCode))
                    snapshot.RoomSensorCode = sCode.GetString();
                break;
            }
        }
    }

    private static void ExtractControllerConfiguration(JsonElement root, HeatPumpSnapshot snapshot)
    {
        if (!root.TryGetProperty("heatPumpControllerConfiguration", out var config))
            return;

        if (config.TryGetProperty("controller", out var controller))
        {
            if (controller.TryGetProperty("connected", out var connected))
                snapshot.ControllerConnected = connected.ValueKind == JsonValueKind.True;

            if (controller.TryGetProperty("state", out var stateArray)
                && stateArray.ValueKind == JsonValueKind.Array)
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

        if (config.TryGetProperty("heatPump", out var heatPump))
        {
            ExtractWeatherCompensation(heatPump, snapshot);
            ExtractFlowTemperature(heatPump, snapshot);
        }

        ExtractZones(root, config, snapshot);
    }

    private static void ExtractWeatherCompensation(JsonElement heatPump, HeatPumpSnapshot snapshot)
    {
        if (!heatPump.TryGetProperty("weatherCompensation", out var weatherComp))
            return;

        bool? wcEnabled = null;
        if (weatherComp.TryGetProperty("enabled", out var wcEnabledEl))
        {
            if (wcEnabledEl.ValueKind == JsonValueKind.True)
                wcEnabled = true;
            else if (wcEnabledEl.ValueKind == JsonValueKind.False)
                wcEnabled = false;
        }

        if (wcEnabled == true)
        {
            snapshot.FlowTempMode = FlowTempMode.WeatherCompensation;
            if (weatherComp.TryGetProperty("currentRange", out var wcRange))
            {
                if (TryGetDecimalValue(wcRange, "minimum", out var wcMin))
                    snapshot.WeatherCompensationMinCelsius = wcMin;
                if (TryGetDecimalValue(wcRange, "maximum", out var wcMax))
                    snapshot.WeatherCompensationMaxCelsius = wcMax;
            }
            snapshot.HeatingFlowTemperatureCelsius = null;
        }
        else if (wcEnabled == false)
        {
            snapshot.FlowTempMode = FlowTempMode.FixedFlow;
            snapshot.WeatherCompensationMinCelsius = null;
            snapshot.WeatherCompensationMaxCelsius = null;
        }
    }

    private static void ExtractFlowTemperature(JsonElement heatPump, HeatPumpSnapshot snapshot)
    {
        if (!heatPump.TryGetProperty("heatingFlowTemperature", out var flowTemp))
            return;

        if (flowTemp.TryGetProperty("allowableRange", out var flowRange))
        {
            if (TryGetDecimalValue(flowRange, "minimum", out var flowMin))
                snapshot.HeatingFlowTempAllowableMinCelsius = flowMin;
            if (TryGetDecimalValue(flowRange, "maximum", out var flowMax))
                snapshot.HeatingFlowTempAllowableMaxCelsius = flowMax;
        }

        if (snapshot.FlowTempMode == FlowTempMode.FixedFlow)
        {
            if (TryGetDecimalValue(flowTemp, "currentTemperature", out var flowDec))
                snapshot.HeatingFlowTemperatureCelsius = flowDec;
        }
    }

    private static void ExtractZones(JsonElement root, JsonElement config, HeatPumpSnapshot snapshot)
    {
        if (!root.TryGetProperty("heatPumpControllerStatus", out var statusForZone)
            || !config.TryGetProperty("zones", out var configZones)
            || !statusForZone.TryGetProperty("zones", out var statusZones)
            || configZones.ValueKind != JsonValueKind.Array
            || statusZones.ValueKind != JsonValueKind.Array)
            return;

        string? heatingZoneCode = null;
        string? hotWaterZoneCode = null;

        foreach (var cz in configZones.EnumerateArray())
        {
            if (cz.TryGetProperty("configuration", out var czConfig)
                && czConfig.TryGetProperty("zoneType", out var zt)
                && czConfig.TryGetProperty("code", out var czCode))
            {
                var zoneType = zt.GetString();
                if (string.Equals(zoneType, "HEAT", StringComparison.OrdinalIgnoreCase) && heatingZoneCode is null)
                    heatingZoneCode = czCode.GetString();
                else if (string.Equals(zoneType, "HOT_WATER", StringComparison.OrdinalIgnoreCase) && hotWaterZoneCode is null)
                    hotWaterZoneCode = czCode.GetString();
            }
        }

        foreach (var sz in statusZones.EnumerateArray())
        {
            if (!sz.TryGetProperty("zone", out var szZone) || !sz.TryGetProperty("telemetry", out var szTelemetry))
                continue;

            var zoneCode = szZone.GetString();

            if (string.Equals(zoneCode, heatingZoneCode, StringComparison.OrdinalIgnoreCase))
            {
                if (szTelemetry.TryGetProperty("setpointInCelsius", out var hzSetpoint)
                    && hzSetpoint.ValueKind == JsonValueKind.Number
                    && hzSetpoint.TryGetDecimal(out var hzSetpointDec))
                    snapshot.HeatingZoneSetpointCelsius = hzSetpointDec;
                if (szTelemetry.TryGetProperty("mode", out var hzMode))
                    snapshot.HeatingZoneMode = hzMode.GetString();
                if (szTelemetry.TryGetProperty("heatDemand", out var hzHeatDemand))
                    snapshot.HeatingZoneHeatDemand = hzHeatDemand.ValueKind == JsonValueKind.True;
            }
            else if (string.Equals(zoneCode, hotWaterZoneCode, StringComparison.OrdinalIgnoreCase))
            {
                if (szTelemetry.TryGetProperty("setpointInCelsius", out var hwSetpoint)
                    && hwSetpoint.ValueKind == JsonValueKind.Number
                    && hwSetpoint.TryGetDecimal(out var hwSetpointDec))
                    snapshot.HotWaterZoneSetpointCelsius = hwSetpointDec;
                if (szTelemetry.TryGetProperty("mode", out var hwMode))
                    snapshot.HotWaterZoneMode = hwMode.GetString();
                if (szTelemetry.TryGetProperty("heatDemand", out var hwHeatDemand))
                    snapshot.HotWaterZoneHeatDemand = hwHeatDemand.ValueKind == JsonValueKind.True;
            }
        }

        // Fallback: if status zone matching found nothing for hot water,
        // try extracting directly from config zone data
        if (hotWaterZoneCode is not null && !snapshot.HotWaterZoneHeatDemand.HasValue)
        {
            foreach (var cz in configZones.EnumerateArray())
            {
                if (cz.TryGetProperty("configuration", out var czConfig)
                    && czConfig.TryGetProperty("zoneType", out var zt)
                    && string.Equals(zt.GetString(), "HOT_WATER", StringComparison.OrdinalIgnoreCase))
                {
                    if (czConfig.TryGetProperty("heatDemand", out var cfgHeatDemand))
                        snapshot.HotWaterZoneHeatDemand = cfgHeatDemand.ValueKind == JsonValueKind.True;
                    if (czConfig.TryGetProperty("callForHeat", out var cfgCallForHeat) && !snapshot.HotWaterZoneHeatDemand.HasValue)
                        snapshot.HotWaterZoneHeatDemand = cfgCallForHeat.ValueKind == JsonValueKind.True;
                    if (czConfig.TryGetProperty("currentOperation", out var curOp)
                        && curOp.TryGetProperty("setpointInCelsius", out var opSetpoint)
                        && opSetpoint.ValueKind == JsonValueKind.Number
                        && opSetpoint.TryGetDecimal(out var opSetpointDec)
                        && !snapshot.HotWaterZoneSetpointCelsius.HasValue)
                        snapshot.HotWaterZoneSetpointCelsius = opSetpointDec;
                    if (czConfig.TryGetProperty("currentOperation", out var curOp2)
                        && curOp2.TryGetProperty("mode", out var opMode)
                        && string.IsNullOrEmpty(snapshot.HotWaterZoneMode))
                        snapshot.HotWaterZoneMode = opMode.GetString();
                    break;
                }
            }
        }
    }

    private static void ExtractSensorReadingsJson(JsonElement root, HeatPumpSnapshot snapshot)
    {
        if (!root.TryGetProperty("heatPumpControllerStatus", out var status)
            || !status.TryGetProperty("sensors", out var allSensors)
            || allSensors.ValueKind != JsonValueKind.Array)
            return;

        var sensorList = new List<object>();
        foreach (var sensor in allSensors.EnumerateArray())
        {
            var entry = new Dictionary<string, object?>();
            if (sensor.TryGetProperty("code", out var code))
                entry["code"] = code.GetString();
            if (sensor.TryGetProperty("connectivity", out var conn)
                && conn.TryGetProperty("online", out var online))
                entry["online"] = online.ValueKind == JsonValueKind.True;
            if (sensor.TryGetProperty("telemetry", out var tel))
            {
                if (tel.TryGetProperty("temperatureInCelsius", out var t)
                    && t.ValueKind == JsonValueKind.Number
                    && t.TryGetDecimal(out var tempC))
                    entry["tempC"] = tempC;
                if (tel.TryGetProperty("humidityPercentage", out var h)
                    && h.ValueKind == JsonValueKind.Number
                    && h.TryGetDecimal(out var humidity))
                    entry["humidity"] = humidity;
            }
            sensorList.Add(entry);
        }
        if (sensorList.Count > 0)
            snapshot.SensorReadingsJson = JsonSerializer.Serialize(sensorList);
    }

    /// <summary>
    /// Helper for the common pattern of extracting a decimal from a nested { "value": "123.45" } property.
    /// Handles: parent.TryGetProperty(name, out var obj) && obj.TryGetProperty("value", out var val) && decimal.TryParse(...)
    /// </summary>
    private static bool TryGetDecimalValue(JsonElement parent, string propertyName, out decimal result)
    {
        result = 0;
        return parent.TryGetProperty(propertyName, out var obj)
               && obj.TryGetProperty("value", out var val)
               && decimal.TryParse(val.GetString() ?? "", out result);
    }
}
