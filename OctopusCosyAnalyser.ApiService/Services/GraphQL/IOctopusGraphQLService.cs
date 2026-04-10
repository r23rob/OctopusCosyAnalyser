using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.GraphQL.Responses;

namespace OctopusCosyAnalyser.ApiService.Services.GraphQL;

/// <summary>
/// Typed GraphQL service for the Octopus Energy backend API.
/// Uses ZeroQL for compile-time validated queries with strongly-typed responses.
/// Only covers queries available on api.backend.octopus.energy.
/// For public API queries (viewer, smartMeter, etc.), use IOctopusEnergyClient.
/// </summary>
public interface IOctopusGraphQLService
{
    /// <summary>
    /// PRIMARY QUERY - batches 4 queries in one call:
    /// controller status, controller config, live time series, and lifetime performance.
    /// Used by the snapshot worker and /summary endpoint.
    /// </summary>
    Task<HeatPumpStatusAndConfigResponse?> GetHeatPumpStatusAndConfigAsync(
        OctopusAccountSettings settings, string accountNumber, string euid, CancellationToken ct = default);

    /// <summary>
    /// Gets aggregated performance for a date range (single totals, no time buckets).
    /// </summary>
    Task<TimeRangedPerformanceResponse?> GetHeatPumpTimeRangedPerformanceAsync(
        OctopusAccountSettings settings, string accountNumber, string euid,
        DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Gets time-bucketed performance data for charting.
    /// Auto-selects grouping based on date range if no override is provided.
    /// </summary>
    Task<TimeSeriesEntry?[]?> GetHeatPumpTimeSeriesPerformanceAsync(
        OctopusAccountSettings settings, string accountNumber, string euid,
        DateTime from, DateTime to, string? performanceGroupingOverride = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets lifetime performance data (seasonal COP, lifetime totals).
    /// </summary>
    Task<LifetimePerformanceResponse?> GetHeatPumpLifetimePerformanceAsync(
        OctopusAccountSettings settings, string accountNumber, string euid,
        CancellationToken ct = default);

    /// <summary>
    /// Discovers all heat pump controllers at a given location.
    /// </summary>
    Task<ControllerAtLocationResponse?[]?> GetHeatPumpControllersAtLocationAsync(
        OctopusAccountSettings settings, string accountNumber, int propertyId,
        CancellationToken ct = default);
}
