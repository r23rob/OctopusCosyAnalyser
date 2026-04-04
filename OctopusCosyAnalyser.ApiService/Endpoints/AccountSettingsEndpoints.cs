using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class AccountSettingsEndpoints
{
    public static void MapAccountSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings");

        group.MapGet("", async (CosyDbContext db, CancellationToken ct) =>
        {
            var settings = await db.OctopusAccountSettings
                .OrderBy(s => s.AccountNumber)
                .ToListAsync(ct);

            return Results.Ok(settings);
        }).WithName("GetAccountSettings");

        group.MapGet("/{accountNumber}", async (string accountNumber, CosyDbContext db, CancellationToken ct) =>
        {
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber, ct);

            return settings is null
                ? Results.NotFound("Account settings not found")
                : Results.Ok(settings);
        }).WithName("GetAccountSettingsByAccount");

        group.MapPut("", async (AccountSettingsRequest request, CosyDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return Results.BadRequest("Account number is required");

            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest("Email is required");

            if (string.IsNullOrWhiteSpace(request.OctopusPassword))
                return Results.BadRequest("Octopus password is required");

            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == request.AccountNumber, ct);

            if (settings is null)
            {
                settings = new OctopusAccountSettings
                {
                    AccountNumber = request.AccountNumber.Trim(),
                    ApiKey = request.ApiKey?.Trim() ?? string.Empty,
                    Email = request.Email.Trim(),
                    OctopusPassword = request.OctopusPassword.Trim(),
                    AnthropicApiKey = request.AnthropicApiKey?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.OctopusAccountSettings.Add(settings);
            }
            else
            {
                settings.ApiKey = request.ApiKey?.Trim() ?? string.Empty;
                settings.Email = request.Email.Trim();
                settings.OctopusPassword = request.OctopusPassword.Trim();
                settings.AnthropicApiKey = request.AnthropicApiKey?.Trim();
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(settings);
        }).WithName("UpsertAccountSettings");
    }

    public sealed record AccountSettingsRequest(string AccountNumber, string? ApiKey, string Email, string OctopusPassword, string? AnthropicApiKey);
}

