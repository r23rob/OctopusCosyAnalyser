using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Tado;

/// <summary>
/// GET /api/tado/zones — list zones for the saved home, including live temperature, humidity,
/// setpoint, and heat demand. Zone states are fetched in parallel; failures are logged and skipped.
/// </summary>
public static class GetTadoZones
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/zones", HandleAsync)
             .WithName("GetTadoZones");

    private static async Task<IResult> HandleAsync(
        ITadoRepository repo,
        TadoClient tado,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(GetTadoZones));
        var settings = await repo.GetSettingsAsync(ct);
        if (settings is null)
            return Results.BadRequest("Tado settings not configured");

        if (settings.HomeId is null)
            return Results.BadRequest("Home ID not set — save settings with a Home ID first");

        var homeId = settings.HomeId.Value;
        var zonesDoc = await tado.GetZonesAsync(settings.Username, settings.Password, homeId);
        var zones = zonesDoc.RootElement.EnumerateArray().ToList();

        var result = new List<TadoZoneDto>();
        foreach (var zone in zones)
        {
            var zoneId = zone.GetProperty("id").GetInt32();
            var zoneDto = new TadoZoneDto
            {
                Id = zoneId,
                Name = zone.GetProperty("name").GetString() ?? string.Empty,
                Type = zone.GetProperty("type").GetString() ?? string.Empty
            };

            try
            {
                var stateDoc = await tado.GetZoneStateAsync(settings.Username, settings.Password, homeId, zoneId);
                var state = stateDoc.RootElement;

                if (state.TryGetProperty("sensorDataPoints", out var sensorData))
                {
                    if (sensorData.TryGetProperty("insideTemperature", out var temp)
                        && temp.TryGetProperty("celsius", out var celsius))
                        zoneDto.CurrentTemperatureCelsius = celsius.GetDecimal();

                    if (sensorData.TryGetProperty("humidity", out var humidity)
                        && humidity.TryGetProperty("percentage", out var pct))
                        zoneDto.CurrentHumidityPercentage = pct.GetDecimal();
                }

                if (state.TryGetProperty("setting", out var setting)
                    && setting.TryGetProperty("temperature", out var setpoint)
                    && setpoint.TryGetProperty("celsius", out var setpointCelsius))
                    zoneDto.SetpointTemperatureCelsius = setpointCelsius.GetDecimal();

                if (state.TryGetProperty("activityDataPoints", out var activity)
                    && activity.TryGetProperty("heatingPower", out var heatingPower)
                    && heatingPower.TryGetProperty("percentage", out var heatPct))
                    zoneDto.HeatingOn = heatPct.GetDecimal() > 0;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch state for Tado zone {ZoneId}", zoneId);
            }

            result.Add(zoneDto);
        }

        return Results.Ok(result);
    }
}
