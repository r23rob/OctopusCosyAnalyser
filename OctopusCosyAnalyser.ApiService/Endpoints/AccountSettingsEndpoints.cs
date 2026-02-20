using OctopusCosyAnalyser.ApiService.Application.AccountSettings;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class AccountSettingsEndpoints
{
    public static void MapAccountSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings");

        group.MapGet("", async (GetAccountSettingsHandler handler) =>
            Results.Ok(await handler.HandleAsync())
        ).WithName("GetAccountSettings");

        group.MapGet("/{accountNumber}", async (string accountNumber, GetAccountSettingsByAccountHandler handler) =>
        {
            var settings = await handler.HandleAsync(new GetAccountSettingsByAccountQuery(accountNumber));
            return settings is null
                ? Results.NotFound("Account settings not found")
                : Results.Ok(settings);
        }).WithName("GetAccountSettingsByAccount");

        group.MapPut("", async (AccountSettingsRequest request, UpsertAccountSettingsHandler handler) =>
        {
            if (string.IsNullOrWhiteSpace(request.AccountNumber))
                return Results.BadRequest("Account number is required");

            if (string.IsNullOrWhiteSpace(request.ApiKey))
                return Results.BadRequest("API key is required");

            var settings = await handler.HandleAsync(new UpsertAccountSettingsCommand(request.AccountNumber, request.ApiKey));
            return Results.Ok(settings);
        }).WithName("UpsertAccountSettings");
    }

    public sealed record AccountSettingsRequest(string AccountNumber, string ApiKey);
}

