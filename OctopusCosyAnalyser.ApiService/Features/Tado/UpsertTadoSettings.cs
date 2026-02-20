using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Tado;

/// <summary>PUT /api/tado/settings — create or update Tado credentials.</summary>
public static class UpsertTadoSettings
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapPut("/settings", HandleAsync)
             .WithName("UpsertTadoSettings");

    private static async Task<IResult> HandleAsync(
        TadoSettingsRequestDto request,
        ITadoRepository repo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Results.BadRequest("Username is required");

        var settings = await repo.GetSettingsAsync(ct);
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
            await repo.AddAsync(settings, ct);
        }
        else
        {
            settings.Username = request.Username.Trim();
            if (!string.IsNullOrWhiteSpace(request.Password))
                settings.Password = request.Password.Trim();
            settings.HomeId = request.HomeId;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await repo.SaveChangesAsync(ct);
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
