using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Infrastructure.Repositories;

public class AccountSettingsRepository(CosyDbContext db) : IAccountSettingsRepository
{
    public Task<List<OctopusAccountSettings>> GetAllAsync(CancellationToken ct = default)
        => db.OctopusAccountSettings.OrderBy(s => s.AccountNumber).ToListAsync(ct);

    public Task<OctopusAccountSettings?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default)
        => db.OctopusAccountSettings.FirstOrDefaultAsync(s => s.AccountNumber == accountNumber, ct);

    public async Task AddAsync(OctopusAccountSettings settings, CancellationToken ct = default)
        => await db.OctopusAccountSettings.AddAsync(settings, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
