using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class AccountSettingsEndpoints
{
    public static void MapAccountSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings");

        group.MapGet("", async (CosyDbContext db) =>
        {
            var settings = await db.OctopusAccountSettings
                .OrderBy(s => s.AccountNumber)
                .ToListAsync();

            return Results.Ok(settings.Select(ToDto).ToArray());
        }).WithName("GetAccountSettings");

        group.MapGet("/{accountNumber}", async (string accountNumber, CosyDbContext db) =>
        {
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber);

            return settings is null
                ? Results.NotFound("Account settings not found")
                : Results.Ok(ToDto(settings));
        }).WithName("GetAccountSettingsByAccount");

        group.MapPut("", async (AccountSettingsRequest request, CosyDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return Results.BadRequest("Account number is required");

            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest("Email is required");

            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == request.AccountNumber);

            if (settings is null)
            {
                if (string.IsNullOrWhiteSpace(request.OctopusPassword))
                    return Results.BadRequest("Octopus password is required for initial setup");

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
                // Only overwrite secrets when the caller provides a new value
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                    settings.ApiKey = request.ApiKey.Trim();
                settings.Email = request.Email.Trim();
                if (!string.IsNullOrWhiteSpace(request.OctopusPassword))
                    settings.OctopusPassword = request.OctopusPassword.Trim();
                if (!string.IsNullOrWhiteSpace(request.AnthropicApiKey))
                    settings.AnthropicApiKey = request.AnthropicApiKey.Trim();
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Results.Ok(ToDto(settings));
        }).WithName("UpsertAccountSettings");
    }

    private static AccountSettingsDto ToDto(OctopusAccountSettings s) => new()
    {
        Id = s.Id,
        AccountNumber = s.AccountNumber,
        HasApiKey = !string.IsNullOrWhiteSpace(s.ApiKey),
        Email = s.Email,
        HasOctopusPassword = !string.IsNullOrWhiteSpace(s.OctopusPassword),
        HasAnthropicApiKey = !string.IsNullOrWhiteSpace(s.AnthropicApiKey),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    public sealed record AccountSettingsRequest(string AccountNumber, string? ApiKey, string Email, string OctopusPassword, string? AnthropicApiKey);
}

