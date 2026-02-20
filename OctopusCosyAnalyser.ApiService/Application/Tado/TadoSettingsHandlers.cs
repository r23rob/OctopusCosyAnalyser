using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Application.Tado;

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetTadoSettingsQuery;

public class GetTadoSettingsHandler(ITadoRepository repo)
{
    public async Task<TadoSettingsDto?> HandleAsync(CancellationToken ct = default)
    {
        var settings = await repo.GetSettingsAsync(ct);
        if (settings is null) return null;

        return new TadoSettingsDto
        {
            Id = settings.Id,
            Username = settings.Username,
            HomeId = settings.HomeId,
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

// ── Commands ──────────────────────────────────────────────────────────────────

public record UpsertTadoSettingsCommand(string Username, string? Password, long? HomeId);

public sealed class UpsertTadoSettingsResult
{
    public TadoSettingsDto? Settings { get; init; }
    public string? ValidationError { get; init; }
}

public class UpsertTadoSettingsHandler(ITadoRepository repo)
{
    public async Task<UpsertTadoSettingsResult> HandleAsync(UpsertTadoSettingsCommand command, CancellationToken ct = default)
    {
        var settings = await repo.GetSettingsAsync(ct);

        if (settings is null)
        {
            if (string.IsNullOrWhiteSpace(command.Password))
                return new UpsertTadoSettingsResult { ValidationError = "Password is required for initial setup" };

            settings = new TadoSettings
            {
                Username = command.Username.Trim(),
                Password = command.Password.Trim(),
                HomeId = command.HomeId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await repo.AddAsync(settings, ct);
        }
        else
        {
            settings.Username = command.Username.Trim();
            if (!string.IsNullOrWhiteSpace(command.Password))
                settings.Password = command.Password.Trim();
            settings.HomeId = command.HomeId;
            settings.UpdatedAt = DateTime.UtcNow;
        }

        await repo.SaveChangesAsync(ct);

        return new UpsertTadoSettingsResult
        {
            Settings = new TadoSettingsDto
            {
                Id = settings.Id,
                Username = settings.Username,
                HomeId = settings.HomeId,
                CreatedAt = settings.CreatedAt,
                UpdatedAt = settings.UpdatedAt
            }
        };
    }
}
