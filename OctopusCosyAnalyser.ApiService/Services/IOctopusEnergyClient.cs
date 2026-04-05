using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IOctopusEnergyClient
{
    Task<JsonDocument> GetAccountAsync(string email, string password, string accountNumber, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetViewerPropertiesAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpControllerEuidsAsync(string email, string password, string accountNumber, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpDeviceAsync(string email, string password, string accountNumber, int propertyId, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetSmartMeterTelemetryAsync(string email, string password, string deviceId, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpStatusAsync(string email, string password, string accountNumber, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpVariantsAsync(string email, string password, string? make = null, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(string email, string password, string accountNumber, string euid, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpTimeRangedPerformanceAsync(string email, string password, string accountNumber, string euid, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpTimeSeriesPerformanceAsync(string email, string password, string accountNumber, string euid, DateTime from, DateTime to, string? performanceGroupingOverride = null, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetHeatPumpControllersAtLocationAsync(string email, string password, string accountNumber, int propertyId, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetApplicableRatesAsync(string email, string password, string accountNumber, string mpxn, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default);
    Task<JsonDocument> GetCostOfUsageAsync(string email, string password, string accountNumber, DateTime from, DateTime to, string grouping = "DAY", int? propertyId = null, string? mpxn = null, CancellationToken cancellationToken = default);
    Task<JsonDocument> ExecuteRawQueryAsync(string email, string password, string query, JsonElement? variables = null, CancellationToken cancellationToken = default);
}
