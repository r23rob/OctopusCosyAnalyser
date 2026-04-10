using System.Globalization;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;

namespace OctopusCosyAnalyser.ApiService.Services;

public static class HeatPumpMappingService
{
    public static HeatPumpSummary MapHeatPumpSummary(HeatPumpStatusAndConfigResponse response)
    {
        return new HeatPumpSummary
        {
            ControllerStatus = MapControllerStatus(response.ControllerStatus),
            ControllerConfiguration = MapControllerConfiguration(response.ControllerConfig),
            LivePerformance = MapLivePerformance(response.TimeSeries),
            LifetimePerformance = MapLifetimePerformance(response.Lifetime)
        };
    }

    private static HeatPumpControllerStatus? MapControllerStatus(ControllerStatusResponse? status)
    {
        if (status is null) return null;

        return new HeatPumpControllerStatus
        {
            Sensors = status.Sensors?
                .Where(s => s is not null)
                .Select(s => MapSensor(s!))
                .ToList() ?? [],
            Zones = status.Zones?
                .Where(z => z is not null)
                .Select(z => MapZoneStatus(z!))
                .ToList() ?? []
        };
    }

    private static HeatPumpSensor MapSensor(SensorStatusResponse sensor)
    {
        return new HeatPumpSensor
        {
            Code = sensor.Code,
            Connectivity = sensor.Connectivity is { } conn
                ? new HeatPumpConnectivity
                {
                    Online = conn.Online,
                    RetrievedAt = conn.RetrievedAt?.ToString("o")
                }
                : null,
            Telemetry = sensor.Telemetry is { } tel
                ? new HeatPumpTelemetry
                {
                    TemperatureInCelsius = tel.TemperatureInCelsius is { } t ? (decimal)t : null,
                    HumidityPercentage = tel.HumidityPercentage is { } h ? h : null,
                    RetrievedAt = tel.RetrievedAt?.ToString("o")
                }
                : null
        };
    }

    private static HeatPumpZoneStatus MapZoneStatus(ZoneStatusResponse zone)
    {
        return new HeatPumpZoneStatus
        {
            Zone = ToWireFormat(zone.Zone),
            Telemetry = zone.Telemetry is { } tel
                ? new HeatPumpZoneTelemetry
                {
                    SetpointInCelsius = tel.SetpointInCelsius is { } sp ? (decimal)sp : null,
                    Mode = ToWireFormat(tel.Mode),
                    RelaySwitchedOn = tel.RelaySwitchedOn,
                    HeatDemand = tel.HeatDemand,
                    RetrievedAt = tel.RetrievedAt?.ToString("o")
                }
                : null
        };
    }

    private static HeatPumpControllerConfiguration? MapControllerConfiguration(ControllerConfigResponse? config)
    {
        if (config is null) return null;

        return new HeatPumpControllerConfiguration
        {
            Controller = config.Controller is { } ctrl
                ? new HeatPumpController
                {
                    State = ctrl.State?
                        .Where(s => s.HasValue)
                        .Select(s => ToWireFormat(s!.Value))
                        .ToList() ?? [],
                    HeatPumpTimezone = ctrl.HeatPumpTimezone,
                    Connected = ctrl.Connected
                }
                : null,
            HeatPump = config.HeatPump is { } hp ? MapHeatPumpDetails(hp) : null,
            Zones = config.Zones?
                .Where(z => z is not null)
                .Select(z => MapZoneConfiguration(z!))
                .ToList() ?? []
        };
    }

    private static HeatPumpDetails MapHeatPumpDetails(HeatPumpConfigResponse hp)
    {
        return new HeatPumpDetails
        {
            SerialNumber = hp.SerialNumber,
            Model = hp.Model,
            HardwareVersion = hp.HardwareVersion,
            MaxWaterSetpoint = hp.MaxWaterSetpoint is { } max ? (int)max : null,
            MinWaterSetpoint = hp.MinWaterSetpoint is { } min ? (int)min : null,
            HeatingFlowTemperature = hp.HeatingFlowTemperature is { } flow
                ? new HeatPumpHeatingFlowTemperature
                {
                    CurrentTemperature = MapMeasurement(flow.CurrentTemperature),
                    AllowableRange = MapRange(flow.AllowableRange)
                }
                : null,
            WeatherCompensation = hp.WeatherCompensation is { } wc
                ? new HeatPumpWeatherCompensation
                {
                    Enabled = wc.Enabled,
                    CurrentRange = MapRange(wc.CurrentRange)
                }
                : null
        };
    }

    private static HeatPumpZoneConfiguration MapZoneConfiguration(ZoneInfoResponse zone)
    {
        return new HeatPumpZoneConfiguration
        {
            Configuration = zone.Configuration is { } cfg
                ? new HeatPumpZoneConfig
                {
                    Code = ToWireFormat(cfg.Code),
                    ZoneType = ToWireFormat(cfg.ZoneType),
                    Enabled = cfg.Enabled,
                    DisplayName = cfg.DisplayName,
                    PrimarySensor = cfg.PrimarySensor,
                    CurrentOperation = cfg.CurrentOperation is { } op
                        ? new HeatPumpCurrentOperation
                        {
                            Mode = ToWireFormat(op.Mode),
                            SetpointInCelsius = op.SetpointInCelsius is { } sp ? (decimal)sp : null,
                            Action = ToWireFormat(op.Action),
                            End = op.End?.ToString("o")
                        }
                        : null,
                    CallForHeat = cfg.CallForHeat,
                    HeatDemand = cfg.HeatDemand,
                    Emergency = cfg.Emergency,
                    // Sensor configuration is not fetched via ZeroQL (union type limitation)
                    Sensors = []
                }
                : null
        };
    }

    private static HeatPumpLivePerformance? MapLivePerformance(TimeSeriesEntry?[]? timeSeries)
    {
        if (timeSeries is not { Length: > 0 })
            return null;

        // Select the last non-null entry — the schema allows nullable elements in the list
        var live = Array.FindLast(timeSeries, entry => entry is not null);
        if (live is null)
            return null;

        // COP is not returned by the API — compute client-side
        string? cop = null;
        var eIn = live.EnergyInput?.Value;
        var eOut = live.EnergyOutput?.Value;
        if (eIn is > 0 && eOut.HasValue)
            cop = (eOut.Value / eIn.Value).ToString("F2", CultureInfo.InvariantCulture);

        return new HeatPumpLivePerformance
        {
            CoefficientOfPerformance = cop,
            OutdoorTemperature = MapMeasurement(live.OutdoorTemperature),
            HeatOutput = MapMeasurement(live.EnergyOutput),
            PowerInput = MapMeasurement(live.EnergyInput),
            ReadAt = live.StartAt.ToString("o")
        };
    }

    private static HeatPumpLifetimePerformance? MapLifetimePerformance(LifetimePerformanceResponse? lifetime)
    {
        if (lifetime is null) return null;

        return new HeatPumpLifetimePerformance
        {
            SeasonalCoefficientOfPerformance = lifetime.SeasonalCoefficientOfPerformance?.ToString(CultureInfo.InvariantCulture),
            HeatOutput = MapMeasurement(lifetime.HeatOutput),
            EnergyInput = MapMeasurement(lifetime.EnergyInput),
            ReadAt = lifetime.ReadAt.ToString("o")
        };
    }

    private static HeatPumpValueAndUnit? MapMeasurement(MeasurementResponse? measurement)
    {
        if (measurement is null) return null;

        return new HeatPumpValueAndUnit
        {
            Value = measurement.Value?.ToString(CultureInfo.InvariantCulture),
            Unit = null // Unit is implicit in ZeroQL responses (kWh for energy, °C for temp)
        };
    }

    private static HeatPumpAllowableRange? MapRange(TemperatureRangeResponse? range)
    {
        if (range is null) return null;

        return new HeatPumpAllowableRange
        {
            Minimum = MapMeasurement(range.Minimum),
            Maximum = MapMeasurement(range.Maximum)
        };
    }

    /// <summary>
    /// Converts a ZeroQL enum value to its GraphQL wire format (SCREAMING_SNAKE_CASE).
    /// ZeroQL generates PascalCase C# enums from SCREAMING_SNAKE GraphQL enums.
    /// </summary>
    private static string? ToWireFormat<T>(T? value) where T : struct, Enum
        => value.HasValue ? ToWireFormat(value.Value) : null;

    private static string ToWireFormat<T>(T value) where T : struct, Enum
    {
        var name = value.ToString();
        // Convert PascalCase to SCREAMING_SNAKE_CASE
        // e.g. "NormalMode" → "NORMAL_MODE", "Heat" → "HEAT", "Zone1" → "ZONE_1"
        return string.Create(name.Length * 2, name, (span, src) =>
        {
            var pos = 0;
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (i > 0 && (char.IsUpper(c) || (char.IsDigit(c) && !char.IsDigit(src[i - 1]))))
                {
                    span[pos++] = '_';
                }
                span[pos++] = char.ToUpperInvariant(c);
            }
        }).TrimEnd('\0');
    }
}
