using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Tado;

/// <summary>GET /api/tado/homes — list Tado homes associated with the saved account.</summary>
public static class GetTadoHomes
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/homes", HandleAsync)
             .WithName("GetTadoHomes");

    private static async Task<IResult> HandleAsync(
        ITadoRepository repo,
        TadoClient tado,
        CancellationToken ct)
    {
        var settings = await repo.GetSettingsAsync(ct);
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
    }
}
