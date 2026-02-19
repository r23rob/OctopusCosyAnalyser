namespace OctopusCosyAnalyser.Shared.Models;

/// <summary>
/// Complete heat pump summary returned by /api/heatpump/summary/{deviceId}.
/// Combines controller status, controller configuration, live performance, and lifetime performance.
/// </summary>
public sealed class HeatPumpSummaryDto
{
    public HeatPumpControllerStatusDto? ControllerStatus { get; set; }
    public HeatPumpControllerConfigurationDto? ControllerConfiguration { get; set; }
    public HeatPumpLivePerformanceDto? LivePerformance { get; set; }
    public HeatPumpLifetimePerformanceDto? LifetimePerformance { get; set; }
}

// ── Controller Status ────────────────────────────────────────────────

public sealed class HeatPumpControllerStatusDto
{
    public List<HeatPumpSensorDto> Sensors { get; set; } = [];
    public List<HeatPumpZoneStatusDto> Zones { get; set; } = [];
}

public sealed class HeatPumpSensorDto
{
    public string? Code { get; set; }
    public HeatPumpConnectivityDto? Connectivity { get; set; }
    public HeatPumpTelemetryDto? Telemetry { get; set; }
}

public sealed class HeatPumpConnectivityDto
{
    public bool? Online { get; set; }
    public string? RetrievedAt { get; set; }
}

public sealed class HeatPumpTelemetryDto
{
    public decimal? TemperatureInCelsius { get; set; }
    public decimal? HumidityPercentage { get; set; }
    public string? RetrievedAt { get; set; }
}

public sealed class HeatPumpZoneStatusDto
{
    public string? Zone { get; set; }
    public HeatPumpZoneTelemetryDto? Telemetry { get; set; }
}

public sealed class HeatPumpZoneTelemetryDto
{
    public decimal? SetpointInCelsius { get; set; }
    public string? Mode { get; set; }
    public bool? RelaySwitchedOn { get; set; }
    public bool? HeatDemand { get; set; }
    public string? RetrievedAt { get; set; }
}

// ── Controller Configuration ─────────────────────────────────────────

public sealed class HeatPumpControllerConfigurationDto
{
    public HeatPumpControllerDto? Controller { get; set; }
    public HeatPumpDetailsDto? HeatPump { get; set; }
    public List<HeatPumpZoneConfigurationDto> Zones { get; set; } = [];
}

public sealed class HeatPumpControllerDto
{
    public List<string> State { get; set; } = [];
    public string? HeatPumpTimezone { get; set; }
    public bool? Connected { get; set; }
}

public sealed class HeatPumpDetailsDto
{
    public string? SerialNumber { get; set; }
    public string? Model { get; set; }
    public string? HardwareVersion { get; set; }
    public int? MaxWaterSetpoint { get; set; }
    public int? MinWaterSetpoint { get; set; }
    public HeatPumpHeatingFlowTemperatureDto? HeatingFlowTemperature { get; set; }
    public HeatPumpWeatherCompensationDto? WeatherCompensation { get; set; }
}

public sealed class HeatPumpHeatingFlowTemperatureDto
{
    public HeatPumpValueAndUnitDto? CurrentTemperature { get; set; }
    public HeatPumpAllowableRangeDto? AllowableRange { get; set; }
}

public sealed class HeatPumpWeatherCompensationDto
{
    public bool? Enabled { get; set; }
    public HeatPumpAllowableRangeDto? CurrentRange { get; set; }
}

public sealed class HeatPumpAllowableRangeDto
{
    public HeatPumpValueAndUnitDto? Minimum { get; set; }
    public HeatPumpValueAndUnitDto? Maximum { get; set; }
}

public sealed class HeatPumpValueAndUnitDto
{
    public string? Value { get; set; }
    public string? Unit { get; set; }
}

public sealed class HeatPumpZoneConfigurationDto
{
    public HeatPumpZoneConfigDto? Configuration { get; set; }
}

public sealed class HeatPumpZoneConfigDto
{
    public string? Code { get; set; }
    public string? ZoneType { get; set; }
    public bool? Enabled { get; set; }
    public string? DisplayName { get; set; }
    public string? PrimarySensor { get; set; }
    public HeatPumpCurrentOperationDto? CurrentOperation { get; set; }
    public bool? CallForHeat { get; set; }
    public bool? HeatDemand { get; set; }
    public bool? Emergency { get; set; }
    public List<HeatPumpSensorConfigurationDto> Sensors { get; set; } = [];
}

public sealed class HeatPumpCurrentOperationDto
{
    public string? Mode { get; set; }
    public decimal? SetpointInCelsius { get; set; }
    public string? Action { get; set; }
    public string? End { get; set; }
}

public sealed class HeatPumpSensorConfigurationDto
{
    public string? Code { get; set; }
    public string? DisplayName { get; set; }
    public string? Type { get; set; }
    public bool? Enabled { get; set; }
    public string? FirmwareVersion { get; set; }
    public bool? BoostEnabled { get; set; }
}

// ── Live Performance ─────────────────────────────────────────────────

public sealed class HeatPumpLivePerformanceDto
{
    public string? CoefficientOfPerformance { get; set; }
    public HeatPumpValueAndUnitDto? OutdoorTemperature { get; set; }
    public HeatPumpValueAndUnitDto? HeatOutput { get; set; }
    public HeatPumpValueAndUnitDto? PowerInput { get; set; }
    public string? ReadAt { get; set; }
}

// ── Lifetime Performance ─────────────────────────────────────────────

public sealed class HeatPumpLifetimePerformanceDto
{
    public string? SeasonalCoefficientOfPerformance { get; set; }
    public HeatPumpValueAndUnitDto? HeatOutput { get; set; }
    public HeatPumpValueAndUnitDto? EnergyInput { get; set; }
    public string? ReadAt { get; set; }
}

