using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Application.Interfaces;

/// <summary>
/// Abstraction over the external Octopus Energy heat-pump API.
/// This keeps the Application layer free of HTTP/infrastructure concerns.
/// </summary>
public interface IHeatPumpProvider
{
    Task<JsonDocument> GetHeatPumpStatusAndConfigAsync(string apiKey, string accountNumber, string euid);
}
