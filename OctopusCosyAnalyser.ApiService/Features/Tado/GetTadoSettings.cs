using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Tado;

/// <summary>GET /api/tado/settings — return saved Tado credentials (password excluded).</summary>
public static class GetTadoSettings
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/settings", HandleAsync)
             .WithName("GetTadoSettings");

    private static async Task<IResult> HandleAsync(
        ITadoRepository repo,
        CancellationToken ct)
    {
        var settings = await repo.GetSettingsAsync(ct);
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
}
