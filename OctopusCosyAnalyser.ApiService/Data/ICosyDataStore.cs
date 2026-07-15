using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data;

public record PagedResult<T>(List<T> Items, int TotalCount, string? Cursor);

public interface ICosyDataStore
{
    // Settings
    Task<List<OctopusAccountSettings>> ListSettingsAsync(string ownerId, CancellationToken ct = default);
    Task<OctopusAccountSettings?> GetSettingsAsync(string ownerId, string accountNumber, CancellationToken ct = default);
    Task UpsertSettingsAsync(OctopusAccountSettings settings, CancellationToken ct = default);

    // Devices
    Task<List<HeatPumpDevice>> ListDevicesAsync(string ownerId, bool activeOnly = true, CancellationToken ct = default);
    Task<HeatPumpDevice?> GetDeviceAsync(string ownerId, string deviceId, CancellationToken ct = default);
    Task<HeatPumpDevice?> GetDeviceByAccountAsync(string ownerId, string accountNumber, CancellationToken ct = default);
    Task UpsertDeviceAsync(HeatPumpDevice device, CancellationToken ct = default);
    Task<List<HeatPumpDevice>> ListAllActiveDevicesAsync(CancellationToken ct = default); // worker use

    // Snapshots
    Task<PagedResult<HeatPumpSnapshot>> GetSnapshotsAsync(string deviceId, DateTime from, DateTime to, string? cursor = null, int limit = 10000, CancellationToken ct = default);
    Task<DateTime?> GetLatestSnapshotTimeAsync(string deviceId, CancellationToken ct = default);
    Task PutSnapshotAsync(HeatPumpSnapshot snapshot, CancellationToken ct = default);
    Task PutSnapshotBatchAsync(List<HeatPumpSnapshot> snapshots, CancellationToken ct = default);
    Task<List<HeatPumpSnapshot>> GetSnapshotListAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default); // for AI/aggregation (no pagination)

    // Consumption
    Task<PagedResult<ConsumptionReading>> GetConsumptionAsync(string deviceId, DateTime from, DateTime to, string? cursor = null, int limit = 10000, CancellationToken ct = default);
    Task<HashSet<DateTime>> GetConsumptionTimestampsAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task PutConsumptionBatchAsync(List<ConsumptionReading> readings, CancellationToken ct = default);

    // TimeSeries
    Task<List<HeatPumpTimeSeriesRecord>> GetTimeSeriesAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<HashSet<DateTime>> GetTimeSeriesTimestampsAsync(string deviceId, DateTime from, CancellationToken ct = default);
    Task PutTimeSeriesBatchAsync(List<HeatPumpTimeSeriesRecord> records, CancellationToken ct = default);

    // DailyCost
    Task<List<DailyCostRecord>> GetDailyCostsAsync(string deviceId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Dictionary<DateOnly, DailyCostRecord>> GetDailyCostMapAsync(string deviceId, DateOnly from, CancellationToken ct = default);
    Task<bool> HasAnyCostDataAsync(string deviceId, CancellationToken ct = default);
    Task UpsertDailyCostBatchAsync(List<DailyCostRecord> records, CancellationToken ct = default);

    // TariffRate
    Task<List<TariffRate>> GetTariffRatesAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task UpsertTariffRateBatchAsync(List<TariffRate> rates, CancellationToken ct = default);

    // EnergyInterval
    Task<List<EnergyInterval>> GetEnergyIntervalsAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<Dictionary<DateTime, EnergyInterval>> GetEnergyIntervalMapAsync(string deviceId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<bool> HasAnyIntervalsAsync(string deviceId, CancellationToken ct = default);
    Task<DateTime?> GetLatestIntervalStartAsync(string deviceId, CancellationToken ct = default);
    Task UpsertEnergyIntervalBatchAsync(List<EnergyInterval> intervals, CancellationToken ct = default);
    Task<List<EnergyInterval>> GetNullCostIntervalsAsync(string deviceId, DateTime from, CancellationToken ct = default);
    Task UpdateEnergyIntervalBatchAsync(List<EnergyInterval> intervals, CancellationToken ct = default);

    // DailyCost standing charges (for EnergyIntervalWorker)
    Task<Dictionary<DateOnly, decimal?>> GetStandingChargesAsync(string deviceId, DateOnly from, DateOnly to, CancellationToken ct = default);

    // DataProtection
    Task<List<DataProtectionKey>> GetDataProtectionKeysAsync(CancellationToken ct = default);
    Task PutDataProtectionKeyAsync(DataProtectionKey key, CancellationToken ct = default);
}
