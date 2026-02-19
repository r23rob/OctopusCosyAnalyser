using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

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

            return Results.Ok(settings);
        }).WithName("GetAccountSettings");

        group.MapGet("/{accountNumber}", async (string accountNumber, CosyDbContext db) =>
        {
            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == accountNumber);

            return settings is null
                ? Results.NotFound("Account settings not found")
                : Results.Ok(settings);
        }).WithName("GetAccountSettingsByAccount");

        group.MapPut("", async (AccountSettingsRequest request, CosyDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return Results.BadRequest("Account number is required");

            if (string.IsNullOrWhiteSpace(request.ApiKey))
                return Results.BadRequest("API key is required");

            var settings = await db.OctopusAccountSettings
                .FirstOrDefaultAsync(s => s.AccountNumber == request.AccountNumber);

            if (settings is null)
            {
                settings = new OctopusAccountSettings
                {
                    AccountNumber = request.AccountNumber.Trim(),
                    ApiKey = request.ApiKey.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.OctopusAccountSettings.Add(settings);
            }
            else
            {
                settings.ApiKey = request.ApiKey.Trim();
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            return Results.Ok(settings);
        }).WithName("UpsertAccountSettings");
    }

    public sealed record AccountSettingsRequest(string AccountNumber, string ApiKey);
}

