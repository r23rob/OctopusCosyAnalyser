using System.Text.Json;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface ITariffSyncService
{
    Task SyncRatesAsync(ICosyDataStore store, OctopusAccountSettings settings, HeatPumpDevice device, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<decimal?> GetUnitRateAtAsync(ICosyDataStore store, string deviceId, DateTime timestamp, CancellationToken cancellationToken = default);
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
        ICosyDataStore store,
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
                OwnerId = device.OwnerId,
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

        // Upsert: update existing rates, insert new ones (workers run with no user context).
        var existingRates = (await store.GetTariffRatesAsync(device.DeviceId, from, to, cancellationToken))
            .ToDictionary(r => r.ValidFrom);

        var toUpsert = new List<TariffRate>();
        var newCount = 0;
        var updatedCount = 0;

        foreach (var rate in rates)
        {
            if (existingRates.TryGetValue(rate.ValidFrom, out var existing))
            {
                var changed = false;
                if (existing.UnitRatePence != rate.UnitRatePence || existing.ValidTo != rate.ValidTo)
                {
                    existing.UnitRatePence = rate.UnitRatePence;
                    existing.ValidTo = rate.ValidTo;
                    changed = true;
                }
                // Rescue legacy rows written before tenancy.
                if (string.IsNullOrEmpty(existing.OwnerId))
                {
                    existing.OwnerId = device.OwnerId;
                    changed = true;
                }
                if (changed)
                {
                    toUpsert.Add(existing);
                    updatedCount++;
                }
            }
            else
            {
                toUpsert.Add(rate);
                newCount++;
            }
        }

        if (toUpsert.Count > 0)
        {
            await store.UpsertTariffRateBatchAsync(toUpsert, cancellationToken);
            _logger.LogInformation("Tariff rates sync for device {DeviceId}: {New} new, {Updated} updated",
                device.DeviceId, newCount, updatedCount);
        }
    }

    public async Task<decimal?> GetUnitRateAtAsync(
        ICosyDataStore store,
        string deviceId,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        var rates = await store.GetTariffRatesAsync(deviceId, timestamp.AddDays(-365), timestamp, cancellationToken);

        return rates
            .Where(r => r.ValidFrom <= timestamp && (r.ValidTo == null || r.ValidTo > timestamp))
            .OrderByDescending(r => r.ValidFrom)
            .Select(r => (decimal?)r.UnitRatePence)
            .FirstOrDefault();
    }
}
