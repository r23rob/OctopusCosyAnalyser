namespace OctopusCosyAnalyser.ApiService.Models;

public sealed class HeatPumpSummary
{
    public HeatPumpControllerStatus? ControllerStatus { get; init; }
    public HeatPumpControllerConfiguration? ControllerConfiguration { get; init; }
    public HeatPumpLivePerformance? LivePerformance { get; init; }
    public HeatPumpLifetimePerformance? LifetimePerformance { get; init; }
}

public sealed class HeatPumpControllerStatus
{
    public IReadOnlyList<HeatPumpSensor> Sensors { get; init; } = Array.Empty<HeatPumpSensor>();
    public IReadOnlyList<HeatPumpZoneStatus> Zones { get; init; } = Array.Empty<HeatPumpZoneStatus>();
}

public sealed class HeatPumpSensor
{
    public string? Code { get; init; }
    public HeatPumpConnectivity? Connectivity { get; init; }
    public HeatPumpTelemetry? Telemetry { get; init; }
}

public sealed class HeatPumpConnectivity
{
    public bool? Online { get; init; }
    public string? RetrievedAt { get; init; }
}

public sealed class HeatPumpTelemetry
{
    public decimal? TemperatureInCelsius { get; init; }
    public decimal? HumidityPercentage { get; init; }
    public string? RetrievedAt { get; init; }
}

public sealed class HeatPumpZoneStatus
{
    public string? Zone { get; init; }
    public HeatPumpZoneTelemetry? Telemetry { get; init; }
}

public sealed class HeatPumpZoneTelemetry
{
    public decimal? SetpointInCelsius { get; init; }
    public string? Mode { get; init; }
    public bool? RelaySwitchedOn { get; init; }
    public bool? HeatDemand { get; init; }
    public string? RetrievedAt { get; init; }
}

public sealed class HeatPumpControllerConfiguration
{
    public HeatPumpController? Controller { get; init; }
    public HeatPumpDetails? HeatPump { get; init; }
    public IReadOnlyList<HeatPumpZoneConfiguration> Zones { get; init; } = Array.Empty<HeatPumpZoneConfiguration>();
}

public sealed class HeatPumpController
{
    public IReadOnlyList<string> State { get; init; } = Array.Empty<string>();
    public string? HeatPumpTimezone { get; init; }
    public bool? Connected { get; init; }
}

public sealed class HeatPumpDetails
{
    public string? SerialNumber { get; init; }
    public string? Model { get; init; }
    public string? HardwareVersion { get; init; }
    public int? MaxWaterSetpoint { get; init; }
    public int? MinWaterSetpoint { get; init; }
    public HeatPumpHeatingFlowTemperature? HeatingFlowTemperature { get; init; }
    public HeatPumpWeatherCompensation? WeatherCompensation { get; init; }
}

public sealed class HeatPumpHeatingFlowTemperature
{
    public HeatPumpValueAndUnit? CurrentTemperature { get; init; }
    public HeatPumpAllowableRange? AllowableRange { get; init; }
}

public sealed class HeatPumpWeatherCompensation
{
    public bool? Enabled { get; init; }
    public HeatPumpAllowableRange? CurrentRange { get; init; }
}

public sealed class HeatPumpAllowableRange
{
    public HeatPumpValueAndUnit? Minimum { get; init; }
    public HeatPumpValueAndUnit? Maximum { get; init; }
}

public sealed class HeatPumpValueAndUnit
{
    public string? Value { get; init; }
    public string? Unit { get; init; }
}

public sealed class HeatPumpZoneConfiguration
{
    public HeatPumpZoneConfig? Configuration { get; init; }
}

public sealed class HeatPumpZoneConfig
{
    public string? Code { get; init; }
    public string? ZoneType { get; init; }
    public bool? Enabled { get; init; }
    public string? DisplayName { get; init; }
    public string? PrimarySensor { get; init; }
    public HeatPumpCurrentOperation? CurrentOperation { get; init; }
    public bool? CallForHeat { get; init; }
    public bool? HeatDemand { get; init; }
    public bool? Emergency { get; init; }
    public IReadOnlyList<HeatPumpSensorConfiguration> Sensors { get; init; } = Array.Empty<HeatPumpSensorConfiguration>();
}

public sealed class HeatPumpCurrentOperation
{
    public string? Mode { get; init; }
    public decimal? SetpointInCelsius { get; init; }
    public string? Action { get; init; }
    public string? End { get; init; }
}

public sealed class HeatPumpSensorConfiguration
{
    public string? Code { get; init; }
    public string? DisplayName { get; init; }
    public string? Type { get; init; }
    public bool? Enabled { get; init; }
    public string? FirmwareVersion { get; init; }
    public bool? BoostEnabled { get; init; }
}

public sealed class HeatPumpLivePerformance
{
    public string? CoefficientOfPerformance { get; init; }
    public HeatPumpValueAndUnit? OutdoorTemperature { get; init; }
    public HeatPumpValueAndUnit? HeatOutput { get; init; }
    public HeatPumpValueAndUnit? PowerInput { get; init; }
    public string? ReadAt { get; init; }
}

public sealed class HeatPumpLifetimePerformance
{
    public string? SeasonalCoefficientOfPerformance { get; init; }
    public HeatPumpValueAndUnit? HeatOutput { get; init; }
    public HeatPumpValueAndUnit? EnergyInput { get; init; }
    public string? ReadAt { get; init; }
}

