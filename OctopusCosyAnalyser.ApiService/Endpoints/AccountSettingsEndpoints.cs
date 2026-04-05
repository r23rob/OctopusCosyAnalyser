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

            var authMode = string.IsNullOrWhiteSpace(request.AuthMode) ? "apikey" : request.AuthMode.Trim().ToLowerInvariant();
            if (authMode is not "apikey" and not "password")
                return Results.BadRequest("AuthMode must be 'apikey' or 'password'");

            if (authMode == "password")
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return Results.BadRequest("Email is required for password authentication");
                if (string.IsNullOrWhiteSpace(request.OctopusPassword))
                    return Results.BadRequest("Octopus password is required for password authentication");
            }
            else // apikey
            {
                if (string.IsNullOrWhiteSpace(request.ApiKey))
                    return Results.BadRequest("API key is required for API key authentication");
            }

            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == request.AccountNumber, ct);

            if (settings is null)
            {
                settings = new OctopusAccountSettings
                {
                    AccountNumber = request.AccountNumber.Trim(),
                    ApiKey = request.ApiKey?.Trim() ?? string.Empty,
                    Email = request.Email?.Trim(),
                    OctopusPassword = request.OctopusPassword?.Trim(),
                    AnthropicApiKey = request.AnthropicApiKey?.Trim(),
                    AuthMode = authMode,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.OctopusAccountSettings.Add(settings);
            }
            else
            {
                settings.ApiKey = request.ApiKey?.Trim() ?? string.Empty;
                settings.Email = request.Email?.Trim();
                settings.OctopusPassword = request.OctopusPassword?.Trim();
                settings.AnthropicApiKey = request.AnthropicApiKey?.Trim();
                settings.AuthMode = authMode;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(settings);
        }).WithName("UpsertAccountSettings");
    }

    public sealed record AccountSettingsRequest(string AccountNumber, string? ApiKey, string? Email, string? OctopusPassword, string? AnthropicApiKey, string? AuthMode);
}
