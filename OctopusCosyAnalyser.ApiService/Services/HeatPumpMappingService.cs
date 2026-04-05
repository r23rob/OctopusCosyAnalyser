using System.Text.Json;
using OctopusCosyAnalyser.ApiService.Models;
using static OctopusCosyAnalyser.ApiService.Helpers.JsonHelpers;

namespace OctopusCosyAnalyser.ApiService.Services;

public static class HeatPumpMappingService
{
    public static HeatPumpSummary MapHeatPumpSummary(JsonElement data)
    {
        return new HeatPumpSummary
        {
            ControllerStatus = MapControllerStatus(data),
            ControllerConfiguration = MapControllerConfiguration(data),
            LivePerformance = MapLivePerformance(data),
            LifetimePerformance = MapLifetimePerformance(data)
        };
    }

    public static HeatPumpControllerStatus? MapControllerStatus(JsonElement data)
    {
        if (!data.TryGetProperty("heatPumpControllerStatus", out var status))
            return null;

        var sensors = status.TryGetProperty("sensors", out var sensorsEl)
            ? sensorsEl.EnumerateArray().Select(MapSensor).ToList()
            : new List<HeatPumpSensor>();

        var zones = status.TryGetProperty("zones", out var zonesEl)
            ? zonesEl.EnumerateArray().Select(MapZoneStatus).ToList()
            : new List<HeatPumpZoneStatus>();

        return new HeatPumpControllerStatus
        {
            Sensors = sensors,
            Zones = zones
        };
    }

    public static HeatPumpSensor MapSensor(JsonElement element)
    {
        return new HeatPumpSensor
        {
            Code = GetString(element, "code"),
            Connectivity = element.TryGetProperty("connectivity", out var connectivity)
                ? new HeatPumpConnectivity
                {
                    Online = GetBool(connectivity, "online"),
                    RetrievedAt = GetString(connectivity, "retrievedAt")
                }
                : null,
            Telemetry = element.TryGetProperty("telemetry", out var telemetry)
                ? new HeatPumpTelemetry
                {
                    TemperatureInCelsius = GetDecimal(telemetry, "temperatureInCelsius"),
                    HumidityPercentage = GetDecimal(telemetry, "humidityPercentage"),
                    RetrievedAt = GetString(telemetry, "retrievedAt")
                }
                : null
        };
    }

    public static HeatPumpZoneStatus MapZoneStatus(JsonElement element)
    {
        return new HeatPumpZoneStatus
        {
            Zone = GetString(element, "zone"),
            Telemetry = element.TryGetProperty("telemetry", out var telemetry)
                ? new HeatPumpZoneTelemetry
                {
                    SetpointInCelsius = GetDecimal(telemetry, "setpointInCelsius"),
                    Mode = GetString(telemetry, "mode"),
                    RelaySwitchedOn = GetBool(telemetry, "relaySwitchedOn"),
                    HeatDemand = GetBool(telemetry, "heatDemand"),
                    RetrievedAt = GetString(telemetry, "retrievedAt")
                }
                : null
        };
    }

    public static HeatPumpControllerConfiguration? MapControllerConfiguration(JsonElement data)
    {
        if (!data.TryGetProperty("heatPumpControllerConfiguration", out var configuration))
            return null;

        var controller = configuration.TryGetProperty("controller", out var controllerEl)
            ? new HeatPumpController
            {
                State = GetStringList(controllerEl, "state"),
                HeatPumpTimezone = GetString(controllerEl, "heatPumpTimezone"),
                Connected = GetBool(controllerEl, "connected")
            }
            : null;

        var heatPump = configuration.TryGetProperty("heatPump", out var heatPumpEl)
            ? MapHeatPumpDetails(heatPumpEl)
            : null;

        var zones = configuration.TryGetProperty("zones", out var zonesEl)
            ? zonesEl.EnumerateArray().Select(MapZoneConfiguration).ToList()
            : new List<HeatPumpZoneConfiguration>();

        return new HeatPumpControllerConfiguration
        {
            Controller = controller,
            HeatPump = heatPump,
            Zones = zones
        };
    }

    public static HeatPumpDetails MapHeatPumpDetails(JsonElement element)
    {
        return new HeatPumpDetails
        {
            SerialNumber = GetString(element, "serialNumber"),
            Model = GetString(element, "model"),
            HardwareVersion = GetString(element, "hardwareVersion"),
            MaxWaterSetpoint = GetInt(element, "maxWaterSetpoint"),
            MinWaterSetpoint = GetInt(element, "minWaterSetpoint"),
            HeatingFlowTemperature = element.TryGetProperty("heatingFlowTemperature", out var flow)
                ? new HeatPumpHeatingFlowTemperature
                {
                    CurrentTemperature = MapValueAndUnit(flow, "currentTemperature"),
                    AllowableRange = MapAllowableRange(flow, "allowableRange")
                }
                : null,
            WeatherCompensation = element.TryGetProperty("weatherCompensation", out var weather)
                ? new HeatPumpWeatherCompensation
                {
                    Enabled = GetBool(weather, "enabled"),
                    CurrentRange = MapAllowableRange(weather, "currentRange")
                }
                : null
        };
    }

    public static HeatPumpZoneConfiguration MapZoneConfiguration(JsonElement element)
    {
        return new HeatPumpZoneConfiguration
        {
            Configuration = element.TryGetProperty("configuration", out var config)
                ? MapZoneConfig(config)
                : null
        };
    }

    public static HeatPumpZoneConfig MapZoneConfig(JsonElement config)
    {
        var sensors = config.TryGetProperty("sensors", out var sensorsEl)
            ? sensorsEl.EnumerateArray().Select(MapSensorConfiguration).ToList()
            : new List<HeatPumpSensorConfiguration>();

        return new HeatPumpZoneConfig
        {
            Code = GetString(config, "code"),
            ZoneType = GetString(config, "zoneType"),
            Enabled = GetBool(config, "enabled"),
            DisplayName = GetString(config, "displayName"),
            PrimarySensor = GetString(config, "primarySensor"),
            CurrentOperation = config.TryGetProperty("currentOperation", out var operation)
                ? new HeatPumpCurrentOperation
                {
                    Mode = GetString(operation, "mode"),
                    SetpointInCelsius = GetDecimal(operation, "setpointInCelsius"),
                    Action = GetString(operation, "action"),
                    End = GetString(operation, "end")
                }
                : null,
            CallForHeat = GetBool(config, "callForHeat"),
            HeatDemand = GetBool(config, "heatDemand"),
            Emergency = GetBool(config, "emergency"),
            Sensors = sensors
        };
    }

    public static HeatPumpSensorConfiguration MapSensorConfiguration(JsonElement sensor)
    {
        return new HeatPumpSensorConfiguration
        {
            Code = GetString(sensor, "code"),
            DisplayName = GetString(sensor, "displayName"),
            Type = GetString(sensor, "type"),
            Enabled = GetBool(sensor, "enabled"),
            FirmwareVersion = GetString(sensor, "firmwareVersion"),
            BoostEnabled = GetBool(sensor, "boostEnabled")
        };
    }

    public static HeatPumpLivePerformance? MapLivePerformance(JsonElement data)
    {
        if (!data.TryGetProperty("heatPumpTimeSeriesPerformance", out var liveArray)
            || liveArray.ValueKind != JsonValueKind.Array
            || liveArray.GetArrayLength() == 0)
            return null;

        // Take the most recent bucket (last element in the LIVE time series)
        var live = liveArray.EnumerateArray().Last();

        // COP is not returned by the new API -- compute client-side from energyOutput / energyInput
        string? cop = null;
        if (live.TryGetProperty("energyInput", out var eiEl) && live.TryGetProperty("energyOutput", out var eoEl))
        {
            var eIn = GetDecimal(eiEl, "value");
            var eOut = GetDecimal(eoEl, "value");
            if (eIn is > 0 && eOut.HasValue)
                cop = (eOut.Value / eIn.Value).ToString("F2");
        }

        return new HeatPumpLivePerformance
        {
            CoefficientOfPerformance = cop,
            OutdoorTemperature = MapValueAndUnit(live, "outdoorTemperature"),
            HeatOutput = MapValueAndUnit(live, "energyOutput"),
            PowerInput = MapValueAndUnit(live, "energyInput"),
            ReadAt = GetString(live, "startAt")
        };
    }

    public static HeatPumpLifetimePerformance? MapLifetimePerformance(JsonElement data)
    {
        if (!data.TryGetProperty("heatPumpLifetimePerformance", out var lifetime))
            return null;

        return new HeatPumpLifetimePerformance
        {
            SeasonalCoefficientOfPerformance = GetString(lifetime, "seasonalCoefficientOfPerformance"),
            HeatOutput = MapValueAndUnit(lifetime, "heatOutput"),
            EnergyInput = MapValueAndUnit(lifetime, "energyInput"),
            ReadAt = GetString(lifetime, "readAt")
        };
    }

    public static HeatPumpValueAndUnit? MapValueAndUnit(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var valueAndUnit))
            return null;

        return new HeatPumpValueAndUnit
        {
            Value = GetString(valueAndUnit, "value"),
            Unit = GetString(valueAndUnit, "unit")
        };
    }

    public static HeatPumpAllowableRange? MapAllowableRange(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var range))
            return null;

        return new HeatPumpAllowableRange
        {
            Minimum = MapValueAndUnit(range, "minimum"),
            Maximum = MapValueAndUnit(range, "maximum")
        };
    }
}
