using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Infrastructure;

/// <summary>
/// Adapts OctopusEnergyClient to the IHeatPumpProvider interface so that
/// the Application layer remains free of infrastructure/HTTP concerns.
/// </summary>
public class OctopusHeatPumpProvider(Services.OctopusEnergyClient client) : IHeatPumpProvider
{
    public Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(string apiKey, string accountNumber, string euid)
        => client.GetHeatPumpStatusAndConfigAsync(apiKey, accountNumber, euid);
}
