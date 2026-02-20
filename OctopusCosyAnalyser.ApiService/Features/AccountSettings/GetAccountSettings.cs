using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.AccountSettings;

/// <summary>GET /api/settings — list all account settings.</summary>
public static class GetAccountSettings
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("", HandleAsync)
             .WithName("GetAccountSettings");

    private static async Task<IResult> HandleAsync(
        IAccountSettingsRepository repo,
        CancellationToken ct)
        => Results.Ok(await repo.GetAllAsync(ct));
}
