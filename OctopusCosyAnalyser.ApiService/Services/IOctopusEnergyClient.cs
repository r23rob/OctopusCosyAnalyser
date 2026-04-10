using System.Text.Json;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IOctopusEnergyClient
{
    Task<JsonDocument> GetAccountAsync(OctopusAccountSettings settings, string accountNumber, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetViewerPropertiesAsync(OctopusAccountSettings settings, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpControllerEuidsAsync(OctopusAccountSettings settings, string accountNumber, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpDeviceAsync(OctopusAccountSettings settings, string accountNumber, int propertyId, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(OctopusAccountSettings settings, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetSmartMeterTelemetryAsync(OctopusAccountSettings settings, string deviceId, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpStatusAsync(OctopusAccountSettings settings, string accountNumber, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpVariantsAsync(OctopusAccountSettings settings, string? make = null, CancellationToken cancellationToken = default);
    // Heat pump controller/performance queries migrated to IOctopusGraphQLService (ZeroQL typed client)
    Task<JsonDocument> GetApplicableRatesAsync(OctopusAccountSettings settings, string accountNumber, string mpxn, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetCostOfUsageAsync(OctopusAccountSettings settings, string accountNumber, DateTime from, DateTime to, string grouping = "DAY", int? propertyId = null, string? mpxn = null, CancellationToken cancellationToken = default);
    Task<JsonDocument> ExecuteRawQueryAsync(OctopusAccountSettings settings, string query, JsonElement? variables = null, CancellationToken cancellationToken = default);
}
