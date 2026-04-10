using OctopusCosyAnalyser.ApiService.GraphQL;
using ZeroQL;

namespace OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;

// ── Heat Pump Status & Config (4-in-1 workhorse) ────────────────────

public sealed record HeatPumpStatusAndConfigResponse(
    ControllerStatusResponse? ControllerStatus,
    ControllerConfigResponse? ControllerConfig,
    TimeSeriesEntry?[]? TimeSeries,
    LifetimePerformanceResponse? Lifetime);

// ── Controller Status ────────────────────────────────────────────────

public sealed record ControllerStatusResponse(
    SensorStatusResponse?[]? Sensors,
    ZoneStatusResponse?[]? Zones);

public sealed record SensorStatusResponse(
    string? Code,
    SensorConnectivityResponse? Connectivity,
    SensorTelemetryResponse? Telemetry);

public sealed record SensorConnectivityResponse(
    bool? Online,
    DateTimeOffset? RetrievedAt);

public sealed record SensorTelemetryResponse(
    double? TemperatureInCelsius,
    int? HumidityPercentage,
    int? Rssi,
    double? Voltage,
    DateTimeOffset? RetrievedAt);

public sealed record ZoneStatusResponse(
    Zone? Zone,
    ZoneTelemetryResponse? Telemetry);

public sealed record ZoneTelemetryResponse(
    double? SetpointInCelsius,
    Mode? Mode,
    bool? RelaySwitchedOn,
    bool? HeatDemand,
    DateTimeOffset? RetrievedAt);

// ── Controller Configuration ─────────────────────────────────────────

public sealed record ControllerConfigResponse(
    ControllerDetailResponse? Controller,
    ZoneInfoResponse?[]? Zones,
    HeatPumpConfigResponse? HeatPump);

public sealed record ControllerDetailResponse(
    bool? Connected,
    State?[]? State,
    string? AccessPointPassword,
    string? HeatPumpTimezone,
    DateTimeOffset? LastReset,
    FirmwareResponse? Firmware);

public sealed record FirmwareResponse(
    string? Efr32,
    string? Esp32,
    string? Eui);

public sealed record HeatPumpConfigResponse(
    string? SerialNumber,
    string? Model,
    string? HardwareVersion,
    string?[]? FaultCodes,
    bool? ManifoldEnabled,
    bool? HasHeatPumpCompatibleCylinder,
    double? MaxWaterSetpoint,
    double? MinWaterSetpoint,
    bool? QuieterModeEnabled,
    WeatherCompensationResponse? WeatherCompensation,
    FlowTemperatureResponse? HeatingFlowTemperature);

public sealed record WeatherCompensationResponse(
    bool Enabled,
    TemperatureRangeResponse? CurrentRange,
    TemperatureRangeResponse? AllowableMinRange,
    TemperatureRangeResponse? AllowableMaxRange);

public sealed record FlowTemperatureResponse(
    MeasurementResponse? CurrentTemperature,
    TemperatureRangeResponse? AllowableRange);

public sealed record TemperatureRangeResponse(
    MeasurementResponse? Minimum,
    MeasurementResponse? Maximum);

/// <summary>
/// Generic measurement value. ZeroQL lambdas select only the value field;
/// the unit is implicit from the measurement type (Energy → kWh, Temperature → °C, etc.).
/// </summary>
public sealed record MeasurementResponse(
    decimal? Value);

public sealed record ZoneInfoResponse(
    ZoneConfigurationResponse? Configuration,
    ZoneScheduleResponse?[]? Schedules);

public sealed record ZoneConfigurationResponse(
    Zone? Code,
    ZoneType? ZoneType,
    bool? Enabled,
    string? DisplayName,
    string? PrimarySensor,
    bool? CallForHeat,
    bool? HeatDemand,
    bool? Emergency,
    ZoneOperationResponse? CurrentOperation,
    ZonePreviousOperationResponse? PreviousOperation);

public sealed record ZoneOperationResponse(
    Mode? Mode,
    double? SetpointInCelsius,
    OperationAction? Action,
    DateTimeOffset? End = null);

public sealed record ZonePreviousOperationResponse(
    Mode? Mode,
    double? SetpointInCelsius,
    OperationAction? Action);

public sealed record ZoneScheduleResponse(
    string? Days,
    ZoneScheduleSettingResponse?[]? Settings);

public sealed record ZoneScheduleSettingResponse(
    string? StartTime,
    string? Action,
    double? SetpointInCelsius);

// ── Time Series Performance ──────────────────────────────────────────

public sealed record TimeSeriesEntry(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    MeasurementResponse? EnergyInput,
    MeasurementResponse? EnergyOutput,
    MeasurementResponse? OutdoorTemperature);

// ── Time Ranged Performance ──────────────────────────────────────────

public sealed record TimeRangedPerformanceResponse(
    decimal? CoefficientOfPerformance,
    MeasurementResponse? EnergyInput,
    MeasurementResponse? EnergyOutput);

// ── Lifetime Performance ─────────────────────────────────────────────

public sealed record LifetimePerformanceResponse(
    decimal? SeasonalCoefficientOfPerformance,
    MeasurementResponse? EnergyInput,
    MeasurementResponse? HeatOutput,
    DateTimeOffset ReadAt);

// ── Controllers at Location ──────────────────────────────────────────

public sealed record ControllerAtLocationResponse(
    string? Euid,
    string HeatPumpModel,
    ID PropertyId,
    DateTimeOffset? ProvisionedAt);
