using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Application.Interfaces;

/// <summary>
/// Repository interface for HeatPumpDevice and HeatPumpSnapshot persistence.
/// </summary>
public interface IHeatPumpSnapshotRepository
{
    Task<List<HeatPumpDevice>> GetActiveDevicesAsync(CancellationToken ct = default);
    Task<OctopusAccountSettings?> GetSettingsForAccountAsync(string accountNumber, CancellationToken ct = default);
    Task AddSnapshotAsync(HeatPumpSnapshot snapshot, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
