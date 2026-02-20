using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Application.Interfaces;

/// <summary>
/// Repository interface for HeatPumpEfficiencyRecord persistence.
/// Implemented by Infrastructure; only the Application layer depends on this abstraction.
/// </summary>
public interface IEfficiencyRepository
{
    Task<List<HeatPumpEfficiencyRecord>> GetRecordsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<HeatPumpEfficiencyRecord?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsForDateAsync(DateOnly date, int? excludeId = null, CancellationToken ct = default);
    Task AddAsync(HeatPumpEfficiencyRecord record, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task DeleteAsync(HeatPumpEfficiencyRecord record, CancellationToken ct = default);
}
