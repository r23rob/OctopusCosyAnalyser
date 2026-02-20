using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Application.AccountSettings;

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetAccountSettingsQuery;

public class GetAccountSettingsHandler(IAccountSettingsRepository repo)
{
    public async Task<List<OctopusAccountSettings>> HandleAsync(CancellationToken ct = default)
        => await repo.GetAllAsync(ct);
}

public record GetAccountSettingsByAccountQuery(string AccountNumber);

public class GetAccountSettingsByAccountHandler(IAccountSettingsRepository repo)
{
    public async Task<OctopusAccountSettings?> HandleAsync(GetAccountSettingsByAccountQuery query, CancellationToken ct = default)
        => await repo.GetByAccountNumberAsync(query.AccountNumber, ct);
}

// ── Commands ──────────────────────────────────────────────────────────────────

public record UpsertAccountSettingsCommand(string AccountNumber, string ApiKey);

public class UpsertAccountSettingsHandler(IAccountSettingsRepository repo)
{
    public async Task<OctopusAccountSettings> HandleAsync(UpsertAccountSettingsCommand command, CancellationToken ct = default)
    {
        var settings = await repo.GetByAccountNumberAsync(command.AccountNumber, ct);

        if (settings is null)
        {
            settings = new OctopusAccountSettings
            {
                AccountNumber = command.AccountNumber.Trim(),
                ApiKey = command.ApiKey.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repo.AddAsync(settings, ct);
        }
        else
        {
            settings.ApiKey = command.ApiKey.Trim();
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await repo.SaveChangesAsync(ct);
        return settings;
    }
}
