using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface ITariffSyncService
{
    Task SyncRatesAsync(CosyDbContext db, OctopusAccountSettings settings, HeatPumpDevice device, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal?> GetUnitRateAtAsync(CosyDbContext db, string deviceId, DateTime timestamp, CancellationToken cancellationToken = default);
}

public class TariffSyncService : ITariffSyncService
{
    private readonly IOctopusEnergyClient _client;
    private readonly ILogger<TariffSyncService> _logger;

    public TariffSyncService(IOctopusEnergyClient client, ILogger<TariffSyncService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task SyncRatesAsync(
        CosyDbContext db,
        OctopusAccountSettings settings,
        HeatPumpDevice device,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(device.Mpan))
        {
            _logger.LogWarning("Device {DeviceId} has no MPAN, skipping tariff sync", device.DeviceId);
            return;
        }

        using var result = await _client.GetApplicableRatesAsync(settings, device.AccountNumber, device.Mpan, from, to, cancellationToken);
        var root = result.RootElement;

        if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
        {
            _logger.LogWarning("Tariff rates query returned errors for device {DeviceId}: {Errors}",
                device.DeviceId, errors.ToString());
            return;
        }

        if (!root.TryGetProperty("data", out var data))
            return;

        var applicableRates = data.GetProperty("applicableRates");
        if (applicableRates.ValueKind == JsonValueKind.Null)
            return;

        var edges = applicableRates.GetProperty("edges");
        var rates = new List<TariffRate>();

        foreach (var edge in edges.EnumerateArray())
        {
            var node = edge.GetProperty("node");

            var validFrom = node.GetProperty("validFrom").GetDateTime();
            DateTime? validTo = node.TryGetProperty("validTo", out var validToEl) && validToEl.ValueKind != JsonValueKind.Null
                ? validToEl.GetDateTime()
                : null;
            var value = node.GetProperty("value").GetDecimal();

            rates.Add(new TariffRate
            {
                DeviceId = device.DeviceId,
                ValidFrom = validFrom,
                ValidTo = validTo,
                UnitRatePence = value,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (rates.Count == 0)
        {
            _logger.LogDebug("No tariff rates returned for device {DeviceId} in range {From} to {To}",
                device.DeviceId, from, to);
            return;
        }

        // Upsert: update existing rates, insert new ones
        var existingRates = await db.TariffRates
            .Where(r => r.DeviceId == device.DeviceId && r.ValidFrom >= from && r.ValidFrom <= to)
            .ToDictionaryAsync(r => r.ValidFrom, cancellationToken);

        var newCount = 0;
        var updatedCount = 0;

        foreach (var rate in rates)
        {
            if (existingRates.TryGetValue(rate.ValidFrom, out var existing))
            {
                if (existing.UnitRatePence != rate.UnitRatePence || existing.ValidTo != rate.ValidTo)
                {
                    existing.UnitRatePence = rate.UnitRatePence;
                    existing.ValidTo = rate.ValidTo;
                    updatedCount++;
                }
            }
            else
            {
                db.TariffRates.Add(rate);
                newCount++;
            }
        }

        if (newCount > 0 || updatedCount > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Tariff rates sync for device {DeviceId}: {New} new, {Updated} updated",
                device.DeviceId, newCount, updatedCount);
        }
    }

    public async Task<decimal?> GetUnitRateAtAsync(
        CosyDbContext db,
        string deviceId,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        return await db.TariffRates
            .Where(r => r.DeviceId == deviceId
                && r.ValidFrom <= timestamp
                && (r.ValidTo == null || r.ValidTo > timestamp))
            .Select(r => (decimal?)r.UnitRatePence)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
