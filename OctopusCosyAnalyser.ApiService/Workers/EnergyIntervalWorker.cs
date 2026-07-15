using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class EnergyIntervalWorker : BackgroundService
{
    private readonly ICosyDataStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EnergyIntervalWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(35);
    private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(90);

    // Nightly backfill runs once per day at ~02:00 UTC
    private DateTime _lastNightlyRun = DateTime.MinValue;

    private const int BackfillDays = 365;
    private const int OverlapDays = 7;
    private const int ChunkSizeDays = 1;

    // Only close windows that ended at least this many minutes ago
    private const int WindowSettleMinutes = 5;

    // ICosyDataStore is a singleton and safe to inject directly. ITariffSyncService is
    // Scoped, so we still need a scope factory to resolve it safely from a Singleton-lifetime
    // BackgroundService (registered via AddHostedService in local continuous mode).
    public EnergyIntervalWorker(ICosyDataStore store, IServiceProvider serviceProvider, ILogger<EnergyIntervalWorker> logger)
    {
        _store = store;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Energy Interval Worker started");

        await Task.Delay(_startupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAllDevicesAsync(stoppingToken);
                await TryNightlyBackfillAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during energy interval processing");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Energy Interval Worker stopped");
    }

    /// <summary>
    /// Run-once entry point used by ACA Jobs / scheduled runners.
    /// Runs the regular pass plus the nightly backfill in a single shot.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        await ProcessAllDevicesAsync(ct);
        await TryNightlyBackfillAsync(ct);
    }

    private async Task ProcessAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var tariffService = scope.ServiceProvider.GetRequiredService<ITariffSyncService>();

        // Workers run with no user context — list across all owners.
        var devices = await _store.ListAllActiveDevicesAsync(stoppingToken);

        foreach (var device in devices)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessDeviceAsync(tariffService, device, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process energy intervals for device {DeviceId}", device.DeviceId);
            }
        }
    }

    private async Task ProcessDeviceAsync(
        ITariffSyncService tariffService,
        HeatPumpDevice device,
        CancellationToken stoppingToken)
    {
        var hasExisting = await _store.HasAnyIntervalsAsync(device.DeviceId, stoppingToken);

        DateTime rangeStart;
        if (!hasExisting)
        {
            // First run: backfill 12 months
            rangeStart = DateTime.UtcNow.AddDays(-BackfillDays);
            _logger.LogInformation("First run for device {DeviceId}: backfilling {Days} days of energy intervals",
                device.DeviceId, BackfillDays);

            // Sync tariff rates for the full backfill range before computing intervals
            var settings = await _store.GetSettingsAsync(device.OwnerId!, device.AccountNumber, stoppingToken);
            if (settings is not null)
            {
                _logger.LogInformation("Syncing tariff rates for backfill range ({Days} days) for device {DeviceId}",
                    BackfillDays, device.DeviceId);
                await tariffService.SyncRatesAsync(_store, settings, device, rangeStart, DateTime.UtcNow, stoppingToken);
            }
        }
        else
        {
            // Subsequent runs: process recent windows with overlap for late data
            var latestIntervalStart = await _store.GetLatestIntervalStartAsync(device.DeviceId, stoppingToken);
            rangeStart = (latestIntervalStart ?? DateTime.UtcNow).AddDays(-OverlapDays);
        }

        var cutoff = DateTime.UtcNow.AddMinutes(-WindowSettleMinutes);

        // Process in daily chunks to bound memory
        var currentDate = rangeStart.Date;
        var endDate = cutoff.Date.AddDays(1);

        while (currentDate < endDate)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var chunkStart = currentDate;
            var chunkEnd = currentDate.AddDays(ChunkSizeDays);
            if (chunkEnd > cutoff) chunkEnd = cutoff;

            await ProcessWindowsForRangeAsync(device, chunkStart, chunkEnd, stoppingToken);

            currentDate = currentDate.AddDays(ChunkSizeDays);
        }
    }

    private async Task ProcessWindowsForRangeAsync(
        HeatPumpDevice device,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken stoppingToken)
    {
        var deviceId = device.DeviceId;

        // Generate all 30-min window starts in this range
        var windowStarts = GenerateWindowStarts(rangeStart, rangeEnd);
        if (windowStarts.Count == 0) return;

        var firstWindow = windowStarts[0];
        var lastWindowEnd = windowStarts[^1].AddMinutes(30);

        // Batch-load snapshots for the entire range (read-only, no pagination)
        var snapshots = await _store.GetSnapshotListAsync(deviceId, firstWindow, lastWindowEnd, stoppingToken);

        // Group snapshots by window once — O(n) instead of O(windows * snapshots)
        var snapshotsByWindow = snapshots
            .GroupBy(s => AlignToWindow(s.SnapshotTakenAt))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Batch-load consumption readings for the range via the paged API, collecting all pages
        var consumptionItems = new List<ConsumptionReading>();
        string? consumptionCursor = null;
        do
        {
            var page = await _store.GetConsumptionAsync(deviceId, firstWindow, lastWindowEnd, consumptionCursor, ct: stoppingToken);
            consumptionItems.AddRange(page.Items);
            consumptionCursor = page.Cursor;
        } while (consumptionCursor is not null);

        var consumption = consumptionItems.ToDictionary(c => AlignToWindow(c.ReadAt));

        // Batch-load tariff rates covering the range — avoids N+1 per-window lookups.
        // LookupUnitRate requires rates pre-sorted by ValidFrom descending.
        var tariffRates = (await _store.GetTariffRatesAsync(deviceId, firstWindow, lastWindowEnd, stoppingToken))
            .OrderByDescending(r => r.ValidFrom)
            .ToList();

        // Batch-load standing charges from DailyCostRecords for days in this range
        var firstDate = DateOnly.FromDateTime(firstWindow);
        var lastDate = DateOnly.FromDateTime(lastWindowEnd);
        var standingCharges = await _store.GetStandingChargesAsync(deviceId, firstDate, lastDate, stoppingToken);

        // Load existing intervals for upsert comparison
        var existingIntervals = await _store.GetEnergyIntervalMapAsync(deviceId, firstWindow, lastWindowEnd, stoppingToken);

        var newIntervals = new List<EnergyInterval>();
        var updatedIntervals = new List<EnergyInterval>();

        foreach (var windowStart in windowStarts)
        {
            var windowEnd = windowStart.AddMinutes(30);

            // Look up snapshots from pre-grouped dictionary — O(1)
            snapshotsByWindow.TryGetValue(windowStart, out var windowSnapshots);
            windowSnapshots ??= [];

            // Find consumption reading for this window
            consumption.TryGetValue(windowStart, out var consumptionReading);

            // Look up tariff rate from in-memory list — most recent rate where ValidFrom <= windowStart
            var unitRate = LookupUnitRate(tariffRates, windowStart);

            // Look up standing charge (only for first interval of the day)
            decimal? standingCharge = null;
            if (windowStart.TimeOfDay == TimeSpan.Zero)
            {
                var dateKey = DateOnly.FromDateTime(windowStart);
                standingCharges.TryGetValue(dateKey, out standingCharge);
            }

            // Compute interval values
            var interval = ComputeInterval(device.OwnerId, deviceId, windowStart, windowEnd, windowSnapshots, consumptionReading, unitRate, standingCharge);

            if (existingIntervals.TryGetValue(windowStart, out var existing))
            {
                if (UpdateIfChanged(existing, interval))
                    updatedIntervals.Add(existing);
            }
            else
            {
                newIntervals.Add(interval);
            }
        }

        if (newIntervals.Count > 0)
            await _store.UpsertEnergyIntervalBatchAsync(newIntervals, stoppingToken);
        if (updatedIntervals.Count > 0)
            await _store.UpdateEnergyIntervalBatchAsync(updatedIntervals, stoppingToken);

        if (newIntervals.Count > 0 || updatedIntervals.Count > 0)
        {
            _logger.LogDebug("Device {DeviceId} range {From:yyyy-MM-dd}: {New} new, {Updated} updated intervals",
                deviceId, rangeStart, newIntervals.Count, updatedIntervals.Count);
        }
    }

    /// <summary>
    /// In-memory point-in-time tariff rate lookup. Rates are pre-sorted by ValidFrom descending,
    /// so the first match where ValidFrom &lt;= timestamp is the most recent applicable rate.
    /// </summary>
    private static decimal? LookupUnitRate(List<TariffRate> rates, DateTime timestamp)
    {
        foreach (var rate in rates)
        {
            if (rate.ValidFrom <= timestamp && (rate.ValidTo == null || rate.ValidTo > timestamp))
                return rate.UnitRatePence;
        }
        return null;
    }

    private static EnergyInterval ComputeInterval(
        string? ownerId,
        string deviceId,
        DateTime windowStart,
        DateTime windowEnd,
        List<HeatPumpSnapshot> snapshots,
        ConsumptionReading? consumption,
        decimal? unitRate,
        decimal? standingCharge)
    {
        var snapshotCount = (short)snapshots.Count;

        // Heat pump averages
        decimal? avgCop = null;
        decimal? avgPowerInputKw = null;
        decimal? heatOutputKwh = null;
        decimal? avgOutdoorTempC = null;
        decimal? avgRoomTempC = null;
        decimal? avgFlowTempC = null;
        bool? wasHeating = null;
        bool? wasHotWater = null;

        if (snapshotCount > 0)
        {
            var copsWithValue = snapshots.Where(s => s.CoefficientOfPerformance.HasValue).ToList();
            avgCop = copsWithValue.Count > 0 ? copsWithValue.Average(s => s.CoefficientOfPerformance!.Value) : null;

            var powerInputs = snapshots.Where(s => s.PowerInputKilowatt.HasValue).ToList();
            avgPowerInputKw = powerInputs.Count > 0 ? powerInputs.Average(s => s.PowerInputKilowatt!.Value) : null;

            // Heat output: sum kW * snapshot interval in hours (uses shared constant)
            var heatOutputs = snapshots.Where(s => s.HeatOutputKilowatt.HasValue).ToList();
            heatOutputKwh = heatOutputs.Count > 0
                ? heatOutputs.Sum(s => s.HeatOutputKilowatt!.Value * (decimal)Constants.SnapshotIntervalHours)
                : null;

            var outdoorTemps = snapshots.Where(s => s.OutdoorTemperatureCelsius.HasValue).ToList();
            avgOutdoorTempC = outdoorTemps.Count > 0 ? outdoorTemps.Average(s => s.OutdoorTemperatureCelsius!.Value) : null;

            var roomTemps = snapshots.Where(s => s.RoomTemperatureCelsius.HasValue).ToList();
            avgRoomTempC = roomTemps.Count > 0 ? roomTemps.Average(s => s.RoomTemperatureCelsius!.Value) : null;

            var flowTemps = snapshots.Where(s => s.HeatingFlowTemperatureCelsius.HasValue).ToList();
            avgFlowTempC = flowTemps.Count > 0 ? flowTemps.Average(s => s.HeatingFlowTemperatureCelsius!.Value) : null;

            wasHeating = snapshots.Any(s => s.HeatingZoneHeatDemand == true);
            wasHotWater = snapshots.Any(s => s.HotWaterZoneHeatDemand == true);
        }

        // Consumption
        decimal? consumptionKwh = consumption?.Consumption;
        decimal? demandW = consumption?.Demand;

        // Derived cost: energy cost + standing charge (if first interval of day)
        decimal? energyCostPence = (consumptionKwh.HasValue && unitRate.HasValue)
            ? consumptionKwh.Value * unitRate.Value
            : null;
        decimal? costPence = (energyCostPence.HasValue || standingCharge.HasValue)
            ? (energyCostPence ?? 0m) + (standingCharge ?? 0m)
            : null;

        return new EnergyInterval
        {
            OwnerId = ownerId,
            DeviceId = deviceId,
            IntervalStart = windowStart,
            IntervalEnd = windowEnd,
            ConsumptionKwh = consumptionKwh,
            DemandW = demandW,
            HeatOutputKwh = heatOutputKwh,
            AvgCop = avgCop,
            AvgPowerInputKw = avgPowerInputKw,
            AvgOutdoorTempC = avgOutdoorTempC,
            AvgRoomTempC = avgRoomTempC,
            AvgFlowTempC = avgFlowTempC,
            WasHeating = wasHeating,
            WasHotWater = wasHotWater,
            SnapshotCount = snapshotCount,
            UnitRatePencePerKwh = unitRate,
            StandingChargePence = standingCharge,
            CostPence = costPence,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates an existing interval if any data fields have changed (e.g., late-arriving consumption or tariff).
    /// Returns true if the record was modified.
    /// </summary>
    private static bool UpdateIfChanged(EnergyInterval existing, EnergyInterval computed)
    {
        var changed = false;

        // Rescue legacy rows written before tenancy was added: stamp OwnerId from the
        // freshly-computed row (which carries device.OwnerId).
        if (string.IsNullOrEmpty(existing.OwnerId) && !string.IsNullOrEmpty(computed.OwnerId))
        {
            existing.OwnerId = computed.OwnerId;
            changed = true;
        }

        // Patch null consumption with late-arriving data
        if (!existing.ConsumptionKwh.HasValue && computed.ConsumptionKwh.HasValue)
        {
            existing.ConsumptionKwh = computed.ConsumptionKwh;
            existing.DemandW = computed.DemandW;
            changed = true;
        }

        // Patch null tariff rate
        if (!existing.UnitRatePencePerKwh.HasValue && computed.UnitRatePencePerKwh.HasValue)
        {
            existing.UnitRatePencePerKwh = computed.UnitRatePencePerKwh;
            changed = true;
        }

        // Patch null standing charge
        if (!existing.StandingChargePence.HasValue && computed.StandingChargePence.HasValue)
        {
            existing.StandingChargePence = computed.StandingChargePence;
            changed = true;
        }

        // Patch null snapshot data (if snapshots arrived late or were reprocessed)
        if (existing.SnapshotCount == 0 && computed.SnapshotCount > 0)
        {
            existing.SnapshotCount = computed.SnapshotCount;
            existing.AvgCop = computed.AvgCop;
            existing.AvgPowerInputKw = computed.AvgPowerInputKw;
            existing.HeatOutputKwh = computed.HeatOutputKwh;
            existing.AvgOutdoorTempC = computed.AvgOutdoorTempC;
            existing.AvgRoomTempC = computed.AvgRoomTempC;
            existing.AvgFlowTempC = computed.AvgFlowTempC;
            existing.WasHeating = computed.WasHeating;
            existing.WasHotWater = computed.WasHotWater;
            changed = true;
        }

        // Recompute cost if components now available
        if (!existing.CostPence.HasValue && existing.ConsumptionKwh.HasValue && existing.UnitRatePencePerKwh.HasValue)
        {
            var energyCost = existing.ConsumptionKwh.Value * existing.UnitRatePencePerKwh.Value;
            existing.CostPence = energyCost + (existing.StandingChargePence ?? 0m);
            changed = true;
        }

        if (changed)
            existing.UpdatedAt = DateTime.UtcNow;

        return changed;
    }

    /// <summary>
    /// Nightly backfill: re-process yesterday's intervals to patch late-arriving data,
    /// and refresh tariff rates for the last 7 days.
    /// Runs once per day around 02:00 UTC.
    /// </summary>
    private async Task TryNightlyBackfillAsync(CancellationToken stoppingToken)
    {
        var now = DateTime.UtcNow;
        if (now.Hour < 2 || now.Hour > 3)
            return;

        if (_lastNightlyRun.Date == now.Date)
            return;

        _lastNightlyRun = now;

        _logger.LogInformation("Running nightly energy interval backfill");

        using var scope = _serviceProvider.CreateScope();
        var tariffService = scope.ServiceProvider.GetRequiredService<ITariffSyncService>();

        var devices = await _store.ListAllActiveDevicesAsync(stoppingToken);

        foreach (var device in devices)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                // Refresh tariff rates for the last 7 days
                var settings = await _store.GetSettingsAsync(device.OwnerId!, device.AccountNumber, stoppingToken);

                if (settings is not null)
                {
                    await tariffService.SyncRatesAsync(_store, settings, device,
                        now.AddDays(-7), now, stoppingToken);
                }

                // Re-process yesterday's 48 windows
                var yesterday = now.Date.AddDays(-1);
                await ProcessWindowsForRangeAsync(device, yesterday, yesterday.AddDays(1), stoppingToken);

                // Also patch any intervals from the last 7 days that still have null cost
                var weekAgo = now.AddDays(-7);
                var nullCostIntervals = await _store.GetNullCostIntervalsAsync(device.DeviceId, weekAgo, stoppingToken);

                foreach (var interval in nullCostIntervals)
                {
                    interval.CostPence = interval.ConsumptionKwh!.Value * interval.UnitRatePencePerKwh!.Value
                        + (interval.StandingChargePence ?? 0m);
                    interval.UpdatedAt = DateTime.UtcNow;
                }

                if (nullCostIntervals.Count > 0)
                {
                    await _store.UpdateEnergyIntervalBatchAsync(nullCostIntervals, stoppingToken);
                    _logger.LogInformation("Nightly: patched {Count} null-cost intervals for device {DeviceId}",
                        nullCostIntervals.Count, device.DeviceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nightly backfill failed for device {DeviceId}", device.DeviceId);
            }
        }
    }

    /// <summary>
    /// Generate all 30-minute aligned window start times in the range [rangeStart, rangeEnd).
    /// </summary>
    private static List<DateTime> GenerateWindowStarts(DateTime rangeStart, DateTime rangeEnd)
    {
        var windows = new List<DateTime>();
        var current = AlignToWindow(rangeStart);

        while (current.AddMinutes(30) <= rangeEnd)
        {
            windows.Add(current);
            current = current.AddMinutes(30);
        }

        return windows;
    }

    /// <summary>
    /// Align a timestamp to the start of its 30-minute window.
    /// e.g., 14:17 → 14:00, 14:45 → 14:30
    /// </summary>
    private static DateTime AlignToWindow(DateTime timestamp)
    {
        var minutes = timestamp.Minute >= 30 ? 30 : 0;
        return new DateTime(timestamp.Year, timestamp.Month, timestamp.Day,
            timestamp.Hour, minutes, 0, timestamp.Kind);
    }
}
