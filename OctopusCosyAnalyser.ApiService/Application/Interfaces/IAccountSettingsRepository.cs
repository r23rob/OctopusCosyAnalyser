using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Application.Interfaces;

/// <summary>
/// Repository interface for OctopusAccountSettings persistence.
/// </summary>
public interface IAccountSettingsRepository
{
    Task<List<OctopusAccountSettings>> GetAllAsync(CancellationToken ct = default);
    Task<OctopusAccountSettings?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task AddAsync(OctopusAccountSettings settings, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
