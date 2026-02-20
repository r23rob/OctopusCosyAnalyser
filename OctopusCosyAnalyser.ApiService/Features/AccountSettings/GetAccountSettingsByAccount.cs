using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.AccountSettings;

/// <summary>GET /api/settings/{accountNumber} — get settings for a specific account.</summary>
public static class GetAccountSettingsByAccount
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/{accountNumber}", HandleAsync)
             .WithName("GetAccountSettingsByAccount");

    private static async Task<IResult> HandleAsync(
        string accountNumber,
        IAccountSettingsRepository repo,
        CancellationToken ct)
    {
        var settings = await repo.GetByAccountNumberAsync(accountNumber, ct);
        return settings is null
            ? Results.NotFound("Account settings not found")
            : Results.Ok(settings);
    }
}
