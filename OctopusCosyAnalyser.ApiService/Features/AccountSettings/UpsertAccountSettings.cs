using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Features.AccountSettings;

/// <summary>PUT /api/settings — create or update Octopus account API key.</summary>
public static class UpsertAccountSettings
{
    public sealed record Request(string AccountNumber, string ApiKey);

    public static void Register(RouteGroupBuilder group) =>
        group.MapPut("", HandleAsync)
             .WithName("UpsertAccountSettings");

    private static async Task<IResult> HandleAsync(
        Request request,
        IAccountSettingsRepository repo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            return Results.BadRequest("Account number is required");

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return Results.BadRequest("API key is required");

        var settings = await repo.GetByAccountNumberAsync(request.AccountNumber, ct);
        if (settings is null)
        {
            settings = new OctopusAccountSettings
            {
                AccountNumber = request.AccountNumber.Trim(),
                ApiKey = request.ApiKey.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repo.AddAsync(settings, ct);
        }
        else
        {
            settings.ApiKey = request.ApiKey.Trim();
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await repo.SaveChangesAsync(ct);
        return Results.Ok(settings);
    }
}
