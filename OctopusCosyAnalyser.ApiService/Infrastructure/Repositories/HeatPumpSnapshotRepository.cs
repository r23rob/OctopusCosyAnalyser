using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Infrastructure.Repositories;

public class HeatPumpSnapshotRepository(CosyDbContext db) : IHeatPumpSnapshotRepository
{
    public Task<List<HeatPumpDevice>> GetActiveDevicesAsync(CancellationToken ct = default)
        => db.HeatPumpDevices.Where(d => d.IsActive).ToListAsync(ct);

    public Task<OctopusAccountSettings?> GetSettingsForAccountAsync(string accountNumber, CancellationToken ct = default)
        => db.OctopusAccountSettings.FirstOrDefaultAsync(s => s.AccountNumber == accountNumber, ct);

    public async Task AddSnapshotAsync(HeatPumpSnapshot snapshot, CancellationToken ct = default)
        => await db.HeatPumpSnapshots.AddAsync(snapshot, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
