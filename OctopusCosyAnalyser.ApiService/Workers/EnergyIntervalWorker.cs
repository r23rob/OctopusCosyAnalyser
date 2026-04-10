using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;

namespace OctopusCosyAnalyser.ApiService.Workers;

public class EnergyIntervalWorker : BackgroundService
{
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

    public EnergyIntervalWorker(IServiceProvider serviceProvider, ILogger<EnergyIntervalWorker> logger)
    {
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

    private async Task ProcessAllDevicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
        var tariffService = scope.ServiceProvider.GetRequiredService<ITariffSyncService>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive)
            .ToListAsync(stoppingToken);

        foreach (var device in devices)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessDeviceAsync(db, tariffService, device, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process energy intervals for device {DeviceId}", device.DeviceId);
            }
        }
    }

    private async Task ProcessDeviceAsync(
        CosyDbContext db,
        ITariffSyncService tariffService,
        HeatPumpDevice device,
        CancellationToken stoppingToken)
    {
        var hasExisting = await db.EnergyIntervals
            .AnyAsync(e => e.DeviceId == device.DeviceId, stoppingToken);

        DateTime rangeStart;
        if (!hasExisting)
        {
            // First run: backfill 12 months
            rangeStart = DateTime.UtcNow.AddDays(-BackfillDays);
            _logger.LogInformation("First run for device {DeviceId}: backfilling {Days} days of energy intervals",
                device.DeviceId, BackfillDays);
        }
        else
        {
            // Subsequent runs: process recent windows with overlap for late data
            var latestInterval = await db.EnergyIntervals
                .Where(e => e.DeviceId == device.DeviceId)
                .MaxAsync(e => e.IntervalStart, stoppingToken);

            rangeStart = latestInterval.AddDays(-OverlapDays);
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

            await ProcessWindowsForRangeAsync(db, tariffService, device.DeviceId, chunkStart, chunkEnd, stoppingToken);
            currentDate = currentDate.AddDays(ChunkSizeDays);
        }
    }

    private async Task ProcessWindowsForRangeAsync(
        CosyDbContext db,
        ITariffSyncService tariffService,
        string deviceId,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken stoppingToken)
    {
        // Generate all 30-min window starts in this range
        var windowStarts = GenerateWindowStarts(rangeStart, rangeEnd);
        if (windowStarts.Count == 0) return;

        var firstWindow = windowStarts[0];
        var lastWindowEnd = windowStarts[^1].AddMinutes(30);

        // Batch-load snapshots for the entire range
        var snapshots = await db.HeatPumpSnapshots
            .Where(s => s.DeviceId == deviceId
                && s.SnapshotTakenAt >= firstWindow
                && s.SnapshotTakenAt < lastWindowEnd)
            .OrderBy(s => s.SnapshotTakenAt)
            .ToListAsync(stoppingToken);

        // Batch-load consumption readings for the range
        var consumption = await db.ConsumptionReadings
            .Where(c => c.DeviceId == deviceId
                && c.ReadAt >= firstWindow
                && c.ReadAt < lastWindowEnd)
            .ToDictionaryAsync(c => AlignToWindow(c.ReadAt), stoppingToken);

        // Load existing intervals for upsert
        var existingIntervals = await db.EnergyIntervals
            .Where(e => e.DeviceId == deviceId
                && e.IntervalStart >= firstWindow
                && e.IntervalStart <= windowStarts[windowStarts.Count - 1])
            .ToDictionaryAsync(e => e.IntervalStart, stoppingToken);

        var newCount = 0;
        var updatedCount = 0;

        foreach (var windowStart in windowStarts)
        {
            var windowEnd = windowStart.AddMinutes(30);

            // Find snapshots in this window
            var windowSnapshots = snapshots
                .Where(s => s.SnapshotTakenAt >= windowStart && s.SnapshotTakenAt < windowEnd)
                .ToList();

            // Find consumption reading for this window
            consumption.TryGetValue(windowStart, out var consumptionReading);

            // Look up tariff rate
            var unitRate = await tariffService.GetUnitRateAtAsync(db, deviceId, windowStart, stoppingToken);

            // Compute interval values
            var interval = ComputeInterval(deviceId, windowStart, windowEnd, windowSnapshots, consumptionReading, unitRate);

            if (existingIntervals.TryGetValue(windowStart, out var existing))
            {
                // Update if data has changed (e.g., late-arriving consumption or tariff)
                if (UpdateIfChanged(existing, interval))
                    updatedCount++;
            }
            else
            {
                db.EnergyIntervals.Add(interval);
                newCount++;
            }
        }

        if (newCount > 0 || updatedCount > 0)
        {
            await db.SaveChangesAsync(stoppingToken);
            _logger.LogDebug("Device {DeviceId} range {From:yyyy-MM-dd}: {New} new, {Updated} updated intervals",
                deviceId, rangeStart, newCount, updatedCount);
        }
    }

    private static EnergyInterval ComputeInterval(
        string deviceId,
        DateTime windowStart,
        DateTime windowEnd,
        List<HeatPumpSnapshot> snapshots,
        ConsumptionReading? consumption,
        decimal? unitRate)
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

            // Heat output: sum kW * (interval per snapshot in hours)
            // Each snapshot covers ~15 min = 0.25h
            var heatOutputs = snapshots.Where(s => s.HeatOutputKilowatt.HasValue).ToList();
            heatOutputKwh = heatOutputs.Count > 0
                ? heatOutputs.Sum(s => s.HeatOutputKilowatt!.Value * 0.25m)
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

        // Standing charge: only on first interval of the day
        decimal? standingCharge = null;
        if (windowStart.TimeOfDay == TimeSpan.Zero)
        {
            // Standing charge is set at the day level; the nightly sync populates this
            // from DailyCostRecord if available
        }

        // Derived cost
        decimal? costPence = (consumptionKwh.HasValue && unitRate.HasValue)
            ? consumptionKwh.Value * unitRate.Value
            : null;

        return new EnergyInterval
        {
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
            existing.CostPence = existing.ConsumptionKwh.Value * existing.UnitRatePencePerKwh.Value;
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
        var db = scope.ServiceProvider.GetRequiredService<CosyDbContext>();
        var tariffService = scope.ServiceProvider.GetRequiredService<ITariffSyncService>();
        var client = scope.ServiceProvider.GetRequiredService<IOctopusEnergyClient>();

        var devices = await db.HeatPumpDevices
            .Where(d => d.IsActive)
            .ToListAsync(stoppingToken);

        foreach (var device in devices)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                // Refresh tariff rates for the last 7 days
                var settings = await db.OctopusAccountSettings
                    .FirstOrDefaultAsync(s => s.AccountNumber == device.AccountNumber, stoppingToken);

                if (settings is not null)
                {
                    await tariffService.SyncRatesAsync(db, settings, device,
                        now.AddDays(-7), now, stoppingToken);
                }

                // Re-process yesterday's 48 windows
                var yesterday = now.Date.AddDays(-1);
                await ProcessWindowsForRangeAsync(db, tariffService, device.DeviceId,
                    yesterday, yesterday.AddDays(1), stoppingToken);

                // Also patch any intervals from the last 7 days that still have null cost
                var weekAgo = now.AddDays(-7);
                var nullCostIntervals = await db.EnergyIntervals
                    .Where(e => e.DeviceId == device.DeviceId
                        && e.IntervalStart >= weekAgo
                        && e.ConsumptionKwh.HasValue
                        && e.UnitRatePencePerKwh.HasValue
                        && !e.CostPence.HasValue)
                    .ToListAsync(stoppingToken);

                foreach (var interval in nullCostIntervals)
                {
                    interval.CostPence = interval.ConsumptionKwh!.Value * interval.UnitRatePencePerKwh!.Value;
                    interval.UpdatedAt = DateTime.UtcNow;
                }

                if (nullCostIntervals.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
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
