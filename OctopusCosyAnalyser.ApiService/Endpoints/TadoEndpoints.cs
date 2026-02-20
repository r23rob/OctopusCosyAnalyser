using OctopusCosyAnalyser.ApiService.Application.Tado;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class TadoEndpoints
{
    public static void MapTadoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tado");
        var logger = app.Services.GetRequiredService<ILogger<TadoClient>>();

        // ── Settings ─────────────────────────────────────────────────

        group.MapGet("/settings", async (GetTadoSettingsHandler handler) =>
            Results.Ok(await handler.HandleAsync())
        ).WithName("GetTadoSettings");

        group.MapPut("/settings", async (TadoSettingsRequestDto request, UpsertTadoSettingsHandler handler) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                return Results.BadRequest("Username is required");

            var result = await handler.HandleAsync(new UpsertTadoSettingsCommand(request.Username, request.Password, request.HomeId));

            if (result.ValidationError is not null)
                return Results.BadRequest(result.ValidationError);

            return Results.Ok(result.Settings);
        }).WithName("UpsertTadoSettings");

        // ── Homes ─────────────────────────────────────────────────────

        group.MapGet("/homes", async (Application.Interfaces.ITadoRepository tadoRepo, TadoClient tado) =>
        {
            var settings = await tadoRepo.GetSettingsAsync();
            if (settings is null)
                return Results.BadRequest("Tado settings not configured");

            var me = await tado.GetMeAsync(settings.Username, settings.Password);
            var homes = me.RootElement.GetProperty("homes");

            var result = homes.EnumerateArray().Select(h => new TadoHomeDto
            {
                Id = h.GetProperty("id").GetInt64(),
                Name = h.GetProperty("name").GetString() ?? string.Empty
            }).ToList();

            return Results.Ok(result);
        }).WithName("GetTadoHomes");

        // ── Zones ─────────────────────────────────────────────────────

        group.MapGet("/zones", async (Application.Interfaces.ITadoRepository tadoRepo, TadoClient tado) =>
        {
            var settings = await tadoRepo.GetSettingsAsync();
            if (settings is null)
                return Results.BadRequest("Tado settings not configured");

            var homeId = settings.HomeId;
            if (homeId is null)
                return Results.BadRequest("Home ID not set — save settings with a Home ID first");

            var zonesDoc = await tado.GetZonesAsync(settings.Username, settings.Password, homeId.Value);
            var zones = zonesDoc.RootElement.EnumerateArray().ToList();

            var result = new List<TadoZoneDto>();
            foreach (var zone in zones)
            {
                var zoneId = zone.GetProperty("id").GetInt32();
                var zoneDto = new TadoZoneDto
                {
                    Id = zoneId,
                    Name = zone.GetProperty("name").GetString() ?? string.Empty,
                    Type = zone.GetProperty("type").GetString() ?? string.Empty,
                };

                try
                {
                    var stateDoc = await tado.GetZoneStateAsync(settings.Username, settings.Password, homeId.Value, zoneId);
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

                    if (state.TryGetProperty("setting", out var setting))
                    {
                        if (setting.TryGetProperty("temperature", out var setpoint)
                            && setpoint.TryGetProperty("celsius", out var setpointCelsius))
                            zoneDto.SetpointTemperatureCelsius = setpointCelsius.GetDecimal();
                    }

                    if (state.TryGetProperty("activityDataPoints", out var activity)
                        && activity.TryGetProperty("heatingPower", out var heatingPower)
                        && heatingPower.TryGetProperty("percentage", out var heatPct))
                    {
                        zoneDto.HeatingOn = heatPct.GetDecimal() > 0;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to fetch state for Tado zone {ZoneId}", zoneId);
                    // State unavailable for this zone – return zone with no readings
                }

                result.Add(zoneDto);
            }

            return Results.Ok(result);
        }).WithName("GetTadoZones");
    }
}
