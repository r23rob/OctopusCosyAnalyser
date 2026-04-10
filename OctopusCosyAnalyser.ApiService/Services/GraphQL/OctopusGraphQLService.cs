using OctopusCosyAnalyser.ApiService.GraphQL;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;
using ZeroQL;

namespace OctopusCosyAnalyser.ApiService.Services.GraphQL;

/// <summary>
/// Typed GraphQL service for the Octopus Energy backend API using ZeroQL.
/// All queries are compile-time validated against the downloaded schema.
///
/// ZeroQL lambdas cannot call .ToString() or complex C# logic — they must be pure
/// field selections. Enum values are stored as-is; convert to strings in consumers.
/// </summary>
public class OctopusGraphQLService : IOctopusGraphQLService
{
    private readonly OctopusGraphQLClient _client;
    private readonly ILogger<OctopusGraphQLService> _logger;

    public OctopusGraphQLService(OctopusGraphQLClient client, ILogger<OctopusGraphQLService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<HeatPumpStatusAndConfigResponse?> GetHeatPumpStatusAndConfigAsync(
        OctopusAccountSettings settings, string accountNumber, string euid, CancellationToken ct = default)
    {
        SetContext(settings);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var liveStartAt = now.AddMinutes(-30);
            var liveEndAt = now;

            var result = await _client.Query(
                new { accountNumber, euid = (ID)euid, liveStartAt, liveEndAt },
                static (v, q) => new HeatPumpStatusAndConfigResponse(
                    // 1. Controller Status
                    q.HeatPumpControllerStatus(v.accountNumber, v.euid, status => new ControllerStatusResponse(
                        status.Sensors(s => new SensorStatusResponse(
                            s.Code,
                            s.Connectivity(c => new SensorConnectivityResponse(c.Online, c.RetrievedAt)),
                            s.Telemetry(t => new SensorTelemetryResponse(
                                t.TemperatureInCelsius, t.HumidityPercentage, t.Rssi, t.Voltage, t.RetrievedAt))
                        )),
                        status.Zones(z => new ZoneStatusResponse(
                            z.Zone,
                            z.Telemetry(t => new ZoneTelemetryResponse(
                                t.SetpointInCelsius, t.Mode, t.RelaySwitchedOn, t.HeatDemand, t.RetrievedAt))
                        ))
                    )),

                    // 2. Controller Configuration
                    q.HeatPumpControllerConfiguration(v.accountNumber, v.euid, config => new ControllerConfigResponse(
                        config.Controller(ctrl => new ControllerDetailResponse(
                            ctrl.Connected,
                            ctrl.State,
                            ctrl.AccessPointPassword,
                            ctrl.HeatPumpTimezone,
                            ctrl.LastReset,
                            ctrl.FirmwareConfiguration(fw => new FirmwareResponse(fw.Efr32, fw.Esp32, fw.Eui))
                        )),
                        config.Zones(zi => new ZoneInfoResponse(
                            zi.Configuration(zc => new ZoneConfigurationResponse(
                                zc.Code,
                                zc.ZoneType,
                                zc.Enabled,
                                zc.DisplayName,
                                zc.PrimarySensor,
                                zc.CallForHeat,
                                zc.HeatDemand,
                                zc.Emergency,
                                zc.CurrentOperation(op => new ZoneOperationResponse(
                                    op.Mode, op.SetpointInCelsius, op.Action, op.End)),
                                zc.PreviousOperation(op => new ZonePreviousOperationResponse(
                                    op.Mode, op.SetpointInCelsius, op.Action))
                            )),
                            zi.Schedules(sch => new ZoneScheduleResponse(
                                sch.Days,
                                sch.Settings(s => new ZoneScheduleSettingResponse(
                                    s.StartTime, s.Action, s.SetpointInCelsius))
                            ))
                        )),
                        config.HeatPump(hp => new HeatPumpConfigResponse(
                            hp.SerialNumber,
                            hp.Model,
                            hp.HardwareVersion,
                            hp.FaultCodes,
                            hp.ManifoldEnabled,
                            hp.HasHeatPumpCompatibleCylinder,
                            hp.MaxWaterSetpoint,
                            hp.MinWaterSetpoint,
                            hp.QuieterModeEnabled,
                            hp.WeatherCompensation(wc => new WeatherCompensationResponse(
                                wc.Enabled,
                                wc.CurrentRange(r => new TemperatureRangeResponse(
                                    r.Minimum(t => new MeasurementResponse(t.Value)),
                                    r.Maximum(t => new MeasurementResponse(t.Value)))),
                                wc.AllowableMinimumTemperatureRange(r => new TemperatureRangeResponse(
                                    r.Minimum(t => new MeasurementResponse(t.Value)),
                                    r.Maximum(t => new MeasurementResponse(t.Value)))),
                                wc.AllowableMaximumTemperatureRange(r => new TemperatureRangeResponse(
                                    r.Minimum(t => new MeasurementResponse(t.Value)),
                                    r.Maximum(t => new MeasurementResponse(t.Value))))
                            )),
                            hp.HeatingFlowTemperature(ft => new FlowTemperatureResponse(
                                ft.CurrentTemperature(t => new MeasurementResponse(t.Value)),
                                ft.AllowableRange(r => new TemperatureRangeResponse(
                                    r.Minimum(t => new MeasurementResponse(t.Value)),
                                    r.Maximum(t => new MeasurementResponse(t.Value))))
                            ))
                        ))
                    )),

                    // 3. Time Series Performance (LIVE - last 30 mins)
                    q.HeatPumpTimeSeriesPerformance(
                        v.accountNumber, v.euid, v.liveStartAt, v.liveEndAt, PerformanceGrouping.Live,
                        ts => new TimeSeriesEntry(
                            ts.StartAt, ts.EndAt,
                            ts.EnergyInput(e => new MeasurementResponse(e.Value)),
                            ts.EnergyOutput(e => new MeasurementResponse(e.Value)),
                            ts.OutdoorTemperature(t => new MeasurementResponse(t.Value))
                        )),

                    // 4. Lifetime Performance
                    q.HeatPumpLifetimePerformance(v.accountNumber, v.euid, lt => new LifetimePerformanceResponse(
                        lt.SeasonalCoefficientOfPerformance,
                        lt.EnergyInput(e => new MeasurementResponse(e.Value)),
                        lt.HeatOutput(e => new MeasurementResponse(e.Value)),
                        lt.ReadAt
                    ))
                ),
                cancellationToken: ct);

            LogErrors(result, "GetHeatPumpStatusAndConfig");
            return result.Data;
        }
        finally
        {
            ClearContext();
        }
    }

    public async Task<TimeRangedPerformanceResponse?> GetHeatPumpTimeRangedPerformanceAsync(
        OctopusAccountSettings settings, string accountNumber, string euid,
        DateTime from, DateTime to, CancellationToken ct = default)
    {
        SetContext(settings);
        try
        {
            var startAt = new DateTimeOffset(from.ToUniversalTime(), TimeSpan.Zero);
            var endAt = new DateTimeOffset(to.ToUniversalTime(), TimeSpan.Zero);

            var result = await _client.Query(
                new { accountNumber, euid = (ID)euid, startAt, endAt },
                static (v, q) => q.HeatPumpTimeRangedPerformance(
                    v.accountNumber, v.euid, v.startAt, v.endAt,
                    perf => new TimeRangedPerformanceResponse(
                        perf.CoefficientOfPerformance,
                        perf.EnergyInput(e => new MeasurementResponse(e.Value)),
                        perf.EnergyOutput(e => new MeasurementResponse(e.Value))
                    )),
                cancellationToken: ct);

            LogErrors(result, "GetHeatPumpTimeRangedPerformance");
            return result.Data;
        }
        finally
        {
            ClearContext();
        }
    }

    public async Task<TimeSeriesEntry?[]?> GetHeatPumpTimeSeriesPerformanceAsync(
        OctopusAccountSettings settings, string accountNumber, string euid,
        DateTime from, DateTime to, string? performanceGroupingOverride = null,
        CancellationToken ct = default)
    {
        SetContext(settings);
        try
        {
            var startAt = new DateTimeOffset(from.ToUniversalTime(), TimeSpan.Zero);
            var endAt = new DateTimeOffset(to.ToUniversalTime(), TimeSpan.Zero);

            var grouping = ResolvePerformanceGrouping(from, to, performanceGroupingOverride);

            var result = await _client.Query(
                new { accountNumber, euid = (ID)euid, startAt, endAt, grouping },
                static (v, q) => q.HeatPumpTimeSeriesPerformance(
                    v.accountNumber, v.euid, v.startAt, v.endAt, v.grouping,
                    ts => new TimeSeriesEntry(
                        ts.StartAt, ts.EndAt,
                        ts.EnergyInput(e => new MeasurementResponse(e.Value)),
                        ts.EnergyOutput(e => new MeasurementResponse(e.Value)),
                        ts.OutdoorTemperature(t => new MeasurementResponse(t.Value))
                    )),
                cancellationToken: ct);

            LogErrors(result, "GetHeatPumpTimeSeriesPerformance");
            return result.Data;
        }
        finally
        {
            ClearContext();
        }
    }

    public async Task<LifetimePerformanceResponse?> GetHeatPumpLifetimePerformanceAsync(
        OctopusAccountSettings settings, string accountNumber, string euid,
        CancellationToken ct = default)
    {
        SetContext(settings);
        try
        {
            var result = await _client.Query(
                new { accountNumber, euid = (ID)euid },
                static (v, q) => q.HeatPumpLifetimePerformance(
                    v.accountNumber, v.euid,
                    lt => new LifetimePerformanceResponse(
                        lt.SeasonalCoefficientOfPerformance,
                        lt.EnergyInput(e => new MeasurementResponse(e.Value)),
                        lt.HeatOutput(e => new MeasurementResponse(e.Value)),
                        lt.ReadAt
                    )),
                cancellationToken: ct);

            LogErrors(result, "GetHeatPumpLifetimePerformance");
            return result.Data;
        }
        finally
        {
            ClearContext();
        }
    }

    public async Task<ControllerAtLocationResponse?[]?> GetHeatPumpControllersAtLocationAsync(
        OctopusAccountSettings settings, string accountNumber, int propertyId,
        CancellationToken ct = default)
    {
        SetContext(settings);
        try
        {
            var result = await _client.Query(
                new { accountNumber, propertyId = (ID)propertyId.ToString() },
                static (v, q) => q.HeatPumpControllersAtLocation(
                    v.accountNumber, v.propertyId,
                    cal => new ControllerAtLocationResponse(
                        cal.Controller(c => c.Euid),
                        cal.HeatPumpModel,
                        cal.Location(l => l.PropertyId),
                        cal.ProvisionedAt
                    )),
                cancellationToken: ct);

            LogErrors(result, "GetHeatPumpControllersAtLocation");
            return result.Data;
        }
        finally
        {
            ClearContext();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void SetContext(OctopusAccountSettings settings)
        => OctopusRequestContext.Current = settings;

    private static void ClearContext()
        => OctopusRequestContext.Current = null;

    private static PerformanceGrouping ResolvePerformanceGrouping(DateTime from, DateTime to, string? overrideValue)
    {
        if (!string.IsNullOrEmpty(overrideValue))
        {
            return Enum.TryParse<PerformanceGrouping>(overrideValue, ignoreCase: true, out var parsed)
                ? parsed
                : PerformanceGrouping.Day;
        }

        var duration = to - from;
        return duration.TotalDays switch
        {
            < 1 => PerformanceGrouping.Live,
            <= 2 => PerformanceGrouping.Day,
            <= 60 => PerformanceGrouping.Week,
            <= 365 => PerformanceGrouping.Month,
            _ => PerformanceGrouping.Year
        };
    }

    private void LogErrors<T>(GraphQLResult<T> result, string queryName)
    {
        if (result.Errors is { Length: > 0 })
        {
            foreach (var error in result.Errors)
            {
                _logger.LogWarning("GraphQL error in {Query}: {Message}", queryName, error.Message);
            }
        }
    }
}
