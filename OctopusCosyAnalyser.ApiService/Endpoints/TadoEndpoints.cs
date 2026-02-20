using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;
using System.Text.Json;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class TadoEndpoints
{
    public static void MapTadoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tado");
        var logger = app.Services.GetRequiredService<ILogger<TadoClient>>();

        // ── Settings ─────────────────────────────────────────────────

        group.MapGet("/settings", async (CosyDbContext db) =>
        {
            try
            {
                var settings = await db.TadoSettings.FirstOrDefaultAsync();
                if (settings is null)
                    return Results.Ok((TadoSettingsDto?)null);

                return Results.Ok(new TadoSettingsDto
                {
                    Id = settings.Id,
                    Username = settings.Username,
                    HomeId = settings.HomeId,
                    CreatedAt = settings.CreatedAt,
                    UpdatedAt = settings.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load Tado settings");
                return Results.Problem("An error occurred while loading Tado settings.", statusCode: 500);
            }
        }).WithName("GetTadoSettings");

        group.MapPut("/settings", async (TadoSettingsRequestDto request, CosyDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                return Results.BadRequest("Username is required");

            try
            {
                var settings = await db.TadoSettings.FirstOrDefaultAsync();

                if (settings is null)
                {
                    if (string.IsNullOrWhiteSpace(request.Password))
                        return Results.BadRequest("Password is required for initial setup");

                    settings = new TadoSettings
                    {
                        Username = request.Username.Trim(),
                        Password = request.Password.Trim(),
                        HomeId = request.HomeId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.TadoSettings.Add(settings);
                }
                else
                {
                    settings.Username = request.Username.Trim();
                    if (!string.IsNullOrWhiteSpace(request.Password))
                        settings.Password = request.Password.Trim();
                    settings.HomeId = request.HomeId;
                    settings.UpdatedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();

                return Results.Ok(new TadoSettingsDto
                {
                    Id = settings.Id,
                    Username = settings.Username,
                    HomeId = settings.HomeId,
                    CreatedAt = settings.CreatedAt,
                    UpdatedAt = settings.UpdatedAt
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save Tado settings");
                return Results.Problem("An error occurred while saving Tado settings.", statusCode: 500);
            }
        }).WithName("UpsertTadoSettings");

        // ── Homes ─────────────────────────────────────────────────────

        group.MapGet("/homes", async (CosyDbContext db, TadoClient tado) =>
        {
            var settings = await db.TadoSettings.FirstOrDefaultAsync();
            if (settings is null)
                return Results.BadRequest("Tado settings not configured");

            try
            {
                var me = await tado.GetMeAsync(settings.Username, settings.Password);

                if (!me.RootElement.TryGetProperty("homes", out var homes))
                    return Results.Problem("Tado API response did not contain a 'homes' field.", statusCode: 502);

                var result = homes.EnumerateArray().Select(h => new TadoHomeDto
                {
                    Id = h.GetProperty("id").GetInt64(),
                    Name = h.GetProperty("name").GetString() ?? string.Empty
                }).ToList();

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch Tado homes");
                return Results.Problem($"Failed to reach the Tado API: {ex.Message}", statusCode: 502);
            }
        }).WithName("GetTadoHomes");

        // ── Zones ─────────────────────────────────────────────────────

        group.MapGet("/zones", async (CosyDbContext db, TadoClient tado) =>
        {
            var settings = await db.TadoSettings.FirstOrDefaultAsync();
            if (settings is null)
                return Results.BadRequest("Tado settings not configured");

            var homeId = settings.HomeId;
            if (homeId is null)
                return Results.BadRequest("Home ID not set — save settings with a Home ID first");

            List<JsonElement> zones;
            try
            {
                var zonesDoc = await tado.GetZonesAsync(settings.Username, settings.Password, homeId.Value);
                zones = zonesDoc.RootElement.EnumerateArray().ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch Tado zones for home {HomeId}", homeId.Value);
                return Results.Problem($"Failed to reach the Tado API: {ex.Message}", statusCode: 502);
            }

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
