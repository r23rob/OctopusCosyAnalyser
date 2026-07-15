using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class AccountSettingsEndpoints
{
    public static void MapAccountSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings");

        group.MapGet("", async (ICosyDataStore store, ICurrentUserAccessor currentUser, CancellationToken ct) =>
        {
            var settings = await store.ListSettingsAsync(currentUser.UserId!, ct);

            return Results.Ok(settings
                .OrderBy(s => s.AccountNumber)
                .Select(ToDto)
                .ToArray());
        }).WithName("GetAccountSettings");

        group.MapGet("/{accountNumber}", async (string accountNumber, ICosyDataStore store, ICurrentUserAccessor currentUser, CancellationToken ct) =>
        {
            var settings = await store.GetSettingsAsync(currentUser.UserId!, accountNumber, ct);

            return settings is null
                ? Results.NotFound("Account settings not found")
                : Results.Ok(ToDto(settings));
        }).WithName("GetAccountSettingsByAccount");

        group.MapPut("", async (
            AccountSettingsRequest request,
            ICosyDataStore store,
            ICurrentUserAccessor currentUser,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return Results.BadRequest("Account number is required");

            var authMode = string.IsNullOrWhiteSpace(request.AuthMode) ? "apikey" : request.AuthMode.Trim().ToLowerInvariant();
            if (authMode is not "apikey" and not "password")
                return Results.BadRequest("AuthMode must be 'apikey' or 'password'");

            var userId = currentUser.UserId!;

            var settings = await store.GetSettingsAsync(userId, request.AccountNumber, ct);

            // Validate required fields for new settings or when switching auth modes
            var isNewSettings = settings is null;
            var isSwitchingToPassword = !isNewSettings && authMode == "password" && settings!.AuthMode != "password";
            var isSwitchingToApiKey = !isNewSettings && authMode == "apikey" && settings!.AuthMode != "apikey";

            if (authMode == "password")
            {
                if (string.IsNullOrWhiteSpace(request.Email))
                    return Results.BadRequest("Email is required for password authentication");

                // Only require password for new settings or when switching auth modes
                if ((isNewSettings || isSwitchingToPassword) && string.IsNullOrWhiteSpace(request.OctopusPassword))
                    return Results.BadRequest("Octopus password is required when setting up password authentication");
            }
            else // apikey
            {
                // Only require API key for new settings or when switching auth modes
                if ((isNewSettings || isSwitchingToApiKey) && string.IsNullOrWhiteSpace(request.ApiKey))
                    return Results.BadRequest("API key is required when setting up API key authentication");
            }

            if (settings is null)
            {
                settings = new OctopusAccountSettings
                {
                    OwnerId = userId,
                    AccountNumber = request.AccountNumber.Trim(),
                    ApiKey = request.ApiKey?.Trim() ?? string.Empty,
                    Email = request.Email?.Trim(),
                    OctopusPassword = request.OctopusPassword?.Trim(),
                    AnthropicApiKey = request.AnthropicApiKey?.Trim(),
                    AuthMode = authMode,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }
            else
            {
                // Only overwrite secrets when the caller provides a new value
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                    settings.ApiKey = request.ApiKey.Trim();
                settings.Email = request.Email?.Trim();
                if (!string.IsNullOrWhiteSpace(request.OctopusPassword))
                    settings.OctopusPassword = request.OctopusPassword.Trim();
                if (!string.IsNullOrWhiteSpace(request.AnthropicApiKey))
                    settings.AnthropicApiKey = request.AnthropicApiKey.Trim();
                settings.AuthMode = authMode;
                settings.UpdatedAt = DateTime.UtcNow;
            }

            await store.UpsertSettingsAsync(settings, ct);

            return Results.Ok(ToDto(settings));
        }).WithName("UpsertAccountSettings");
    }

    private static AccountSettingsDto ToDto(OctopusAccountSettings s) => new()
    {
        AccountNumber = s.AccountNumber,
        HasApiKey = !string.IsNullOrWhiteSpace(s.ApiKey),
        Email = s.Email,
        HasOctopusPassword = !string.IsNullOrWhiteSpace(s.OctopusPassword),
        HasAnthropicApiKey = !string.IsNullOrWhiteSpace(s.AnthropicApiKey),
        AuthMode = s.AuthMode,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    public sealed record AccountSettingsRequest(string AccountNumber, string? ApiKey, string? Email, string? OctopusPassword, string? AnthropicApiKey, string? AuthMode);
}
