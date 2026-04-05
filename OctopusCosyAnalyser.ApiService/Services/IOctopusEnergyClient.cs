using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IOctopusEnergyClient
{
    Task<JsonDocument> GetAccountAsync(string email, string password, string accountNumber);
    Task<JsonDocument> GetViewerPropertiesAsync(string email, string password);
    Task<JsonDocument> GetHeatPumpControllerEuidsAsync(string email, string password, string accountNumber);
    Task<JsonDocument> GetHeatPumpDeviceAsync(string email, string password, string accountNumber, int propertyId);
    Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(string email, string password);
    Task<JsonDocument> GetSmartMeterTelemetryAsync(string email, string password, string deviceId);
    Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to);
    Task<JsonDocument> GetHeatPumpStatusAsync(string email, string password, string accountNumber);
    Task<JsonDocument> GetHeatPumpVariantsAsync(string email, string password, string? make = null);
    Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(string email, string password, string accountNumber, string euid);
    Task<JsonDocument> GetHeatPumpTimeRangedPerformanceAsync(string email, string password, string accountNumber, string euid, DateTime from, DateTime to);
    Task<JsonDocument> GetHeatPumpTimeSeriesPerformanceAsync(string email, string password, string accountNumber, string euid, DateTime from, DateTime to, string? performanceGroupingOverride = null);
    Task<JsonDocument> GetHeatPumpControllersAtLocationAsync(string email, string password, string accountNumber, int propertyId);
    Task<JsonDocument> GetApplicableRatesAsync(string email, string password, string accountNumber, string mpxn, DateTime startAt, DateTime endAt);
    Task<JsonDocument> GetCostOfUsageAsync(string email, string password, string accountNumber, DateTime from, DateTime to, string grouping = "DAY", int? propertyId = null, string? mpxn = null);
    Task<JsonDocument> ExecuteRawQueryAsync(string email, string password, string query, JsonElement? variables = null);
}
