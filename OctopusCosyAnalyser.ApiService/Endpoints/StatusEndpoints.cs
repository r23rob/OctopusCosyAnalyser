using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/status", () =>
        {
            var dto = new ApiStatusDto
            {
                CheckedAt = DateTime.UtcNow,
                HasSettings = false,
                OctopusCredentialsConfigured = false,
                OctopusAuthOk = false,
                AnthropicConfigured = false,
                HasDevice = false,
            };
            return Results.Ok(dto);
        }).WithName("GetApiStatus");
    }
}
