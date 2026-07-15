using Microsoft.Extensions.Options;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Options;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/status", async (
            ICosyDataStore store,
            IOctopusEnergyClient octopusClient,
            IOptions<AnthropicOptions> anthropicOptions,
            CancellationToken ct) =>
        {
            var dto = new ApiStatusDto { CheckedAt = DateTime.UtcNow };
            var userId = HttpContextCurrentUserAccessor.FixedUserId;

            var settingsList = await store.ListSettingsAsync(userId, ct);
            var settings = settingsList.FirstOrDefault();

            if (settings is null)
            {
                dto.HasSettings = false;
                dto.OctopusCredentialsConfigured = false;
                dto.OctopusAuthOk = false;
                dto.OctopusAuthError = "No Octopus Energy account configured. Open Settings to add your account number and API key.";
                dto.AnthropicConfigured = !string.IsNullOrWhiteSpace(anthropicOptions.Value.ApiKey);
                dto.AnthropicKeySource = dto.AnthropicConfigured ? "config" : null;
                dto.HasDevice = false;
                return Results.Ok(dto);
            }

            dto.HasSettings = true;
            dto.AccountNumber = settings.AccountNumber;
            dto.AuthMode = settings.AuthMode;

            var hasApiKeyCreds = settings.AuthMode == "apikey" && !string.IsNullOrWhiteSpace(settings.ApiKey);
            var hasPasswordCreds = settings.AuthMode == "password"
                && !string.IsNullOrWhiteSpace(settings.Email)
                && !string.IsNullOrWhiteSpace(settings.OctopusPassword);
            dto.OctopusCredentialsConfigured = hasApiKeyCreds || hasPasswordCreds;

            if (!dto.OctopusCredentialsConfigured)
            {
                dto.OctopusAuthOk = false;
                dto.OctopusAuthError = settings.AuthMode == "password"
                    ? "Email and Octopus password are required for password authentication."
                    : "Octopus API key is missing. Open Settings to add it.";
            }
            else
            {
                var (ok, error) = await octopusClient.ValidateCredentialsAsync(settings, ct);
                dto.OctopusAuthOk = ok;
                dto.OctopusAuthError = error;
            }

            // Anthropic key may come from either the account settings (DB) or global config / ANTHROPIC_API_KEY env var
            if (!string.IsNullOrWhiteSpace(settings.AnthropicApiKey))
            {
                dto.AnthropicConfigured = true;
                dto.AnthropicKeySource = "account";
            }
            else if (!string.IsNullOrWhiteSpace(anthropicOptions.Value.ApiKey))
            {
                dto.AnthropicConfigured = true;
                dto.AnthropicKeySource = "config";
            }
            else
            {
                dto.AnthropicConfigured = false;
                dto.AnthropicKeySource = null;
            }

            var devices = await store.ListDevicesAsync(userId, activeOnly: false, ct);
            dto.HasDevice = devices.Any(d => d.AccountNumber == settings.AccountNumber);

            return Results.Ok(dto);
        }).WithName("GetApiStatus");
    }
}
