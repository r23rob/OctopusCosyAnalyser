using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Infrastructure.Repositories;

public class EfficiencyRepository(CosyDbContext db) : IEfficiencyRepository
{
    public async Task<List<HeatPumpEfficiencyRecord>> GetRecordsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var query = db.HeatPumpEfficiencyRecords.AsQueryable();
        if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
        if (to.HasValue) query = query.Where(r => r.Date <= to.Value);
        return await query.OrderBy(r => r.Date).ToListAsync(ct);
    }

    public Task<HeatPumpEfficiencyRecord?> GetByIdAsync(int id, CancellationToken ct = default)
        => db.HeatPumpEfficiencyRecords.FindAsync([id], ct).AsTask();

    public Task<bool> ExistsForDateAsync(DateOnly date, int? excludeId = null, CancellationToken ct = default)
        => db.HeatPumpEfficiencyRecords
            .AnyAsync(r => r.Date == date && (excludeId == null || r.Id != excludeId), ct);

    public async Task AddAsync(HeatPumpEfficiencyRecord record, CancellationToken ct = default)
        => await db.HeatPumpEfficiencyRecords.AddAsync(record, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public Task DeleteAsync(HeatPumpEfficiencyRecord record, CancellationToken ct = default)
    {
        db.HeatPumpEfficiencyRecords.Remove(record);
        return Task.CompletedTask;
    }
}
