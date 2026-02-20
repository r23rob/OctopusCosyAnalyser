using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Infrastructure.Repositories;

public class TadoRepository(CosyDbContext db) : ITadoRepository
{
    public Task<TadoSettings?> GetSettingsAsync(CancellationToken ct = default)
        => db.TadoSettings.FirstOrDefaultAsync(ct);

    public async Task AddAsync(TadoSettings settings, CancellationToken ct = default)
        => await db.TadoSettings.AddAsync(settings, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
