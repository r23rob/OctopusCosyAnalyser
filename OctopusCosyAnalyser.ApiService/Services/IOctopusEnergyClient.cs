using System.Text.Json;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IOctopusEnergyClient
{
    Task<JsonDocument> GetAccountAsync(OctopusAccountSettings settings, string accountNumber);
    Task<JsonDocument> GetViewerPropertiesAsync(OctopusAccountSettings settings);
    Task<JsonDocument> GetHeatPumpControllerEuidsAsync(OctopusAccountSettings settings, string accountNumber);
    Task<JsonDocument> GetHeatPumpDeviceAsync(OctopusAccountSettings settings, string accountNumber, int propertyId);
    Task<JsonDocument> GetViewerPropertiesWithDevicesAsync(OctopusAccountSettings settings);
    Task<JsonDocument> GetSmartMeterTelemetryAsync(OctopusAccountSettings settings, string deviceId);
    Task<JsonDocument> GetConsumptionHistoryAsync(string apiKey, string mpan, string serialNumber, DateTime from, DateTime to);
    Task<JsonDocument> GetHeatPumpStatusAsync(OctopusAccountSettings settings);
    Task<JsonDocument> GetHeatPumpVariantsAsync(OctopusAccountSettings settings, string? make = null);
    Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(OctopusAccountSettings settings, string accountNumber, string euid);
    Task<JsonDocument> GetHeatPumpTimeRangedPerformanceAsync(OctopusAccountSettings settings, string accountNumber, string euid, DateTime from, DateTime to);
    Task<JsonDocument> GetHeatPumpTimeSeriesPerformanceAsync(OctopusAccountSettings settings, string accountNumber, string euid, DateTime from, DateTime to, string? performanceGroupingOverride = null);
    Task<JsonDocument> GetHeatPumpControllersAtLocationAsync(OctopusAccountSettings settings, string accountNumber, int propertyId);
    Task<JsonDocument> GetApplicableRatesAsync(OctopusAccountSettings settings, string accountNumber, string mpxn, DateTime startAt, DateTime endAt);
    Task<JsonDocument> GetCostOfUsageAsync(OctopusAccountSettings settings, string accountNumber, DateTime from, DateTime to, string grouping = "DAY", int? propertyId = null, string? mpxn = null);
    Task<JsonDocument> ExecuteRawQueryAsync(OctopusAccountSettings settings, string query, JsonElement? variables = null);
}
