using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Application.Interfaces;

/// <summary>
/// Repository interface for TadoSettings persistence.
/// </summary>
public interface ITadoRepository
{
    Task<TadoSettings?> GetSettingsAsync(CancellationToken ct = default);
    Task AddAsync(TadoSettings settings, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
