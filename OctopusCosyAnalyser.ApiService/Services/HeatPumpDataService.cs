using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public class HeatPumpDataService : IHeatPumpDataService
{
    public List<DailyAggregateDto> ComputeDailyAggregates(List<HeatPumpSnapshot> snapshots)
    {
        return snapshots
            .GroupBy(s => DateOnly.FromDateTime(s.SnapshotTakenAt))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var day = g.ToList();
                var heatingSnapshots = day.Where(s => s.HeatingZoneHeatDemand == true).ToList();
                var hotWaterSnapshots = day.Where(s => s.HotWaterZoneHeatDemand == true).ToList();
                var spaceHeatingOnly = day.Where(s => s.HeatingZoneHeatDemand == true && s.HotWaterZoneHeatDemand != true).ToList();

                // Controller state transitions
                var stateTransitions = 0;
                string? prevState = null;
                foreach (var s in day)
                {
                    if (prevState != null && s.ControllerState != prevState)
                        stateTransitions++;
                    prevState = s.ControllerState;
                }

                // Hot water run count (distinct periods of consecutive HW demand)
                var hwRunCount = 0;
                var hwTotalSnapshots = 0;
                var prevHwDemand = false;
                foreach (var s in day)
                {
                    var hwDemand = s.HotWaterZoneHeatDemand == true;
                    if (hwDemand && !prevHwDemand)
                        hwRunCount++;
                    if (hwDemand)
                        hwTotalSnapshots++;
                    prevHwDemand = hwDemand;
                }

                // Flow temp mode (most frequent non-null value for the day)
                var flowTempModeForDay = day.Where(s => s.FlowTempMode != null)
                    .GroupBy(s => s.FlowTempMode!)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                // WC curve range — only meaningful when in WeatherCompensation mode
                var wcSnapshots = day.Where(s => s.FlowTempMode == FlowTempMode.WeatherCompensation).ToList();
                var wcMinMode = wcSnapshots.Where(s => s.WeatherCompensationMinCelsius.HasValue)
                    .GroupBy(s => s.WeatherCompensationMinCelsius!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                var wcMaxMode = wcSnapshots.Where(s => s.WeatherCompensationMaxCelsius.HasValue)
                    .GroupBy(s => s.WeatherCompensationMaxCelsius!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                // Fixed flow snapshots for setpoint average
                var fixedFlowSnapshots = day.Where(s => s.FlowTempMode == FlowTempMode.FixedFlow).ToList();

                var flowTempMinMode = day.Where(s => s.HeatingFlowTempAllowableMinCelsius.HasValue)
                    .GroupBy(s => s.HeatingFlowTempAllowableMinCelsius!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                var flowTempMaxMode = day.Where(s => s.HeatingFlowTempAllowableMaxCelsius.HasValue)
                    .GroupBy(s => s.HeatingFlowTempAllowableMaxCelsius!.Value)
                    .OrderByDescending(g2 => g2.Count())
                    .FirstOrDefault()?.Key;

                return new DailyAggregateDto
                {
                    Date = g.Key,
                    SnapshotCount = day.Count,

                    AvgCopHeating = AvgDecimal(heatingSnapshots, s => s.CoefficientOfPerformance),
                    AvgCopHotWater = AvgDecimal(hotWaterSnapshots, s => s.CoefficientOfPerformance),
                    AvgCopSpaceHeatingOnly = AvgDecimal(spaceHeatingOnly, s => s.CoefficientOfPerformance),

                    TotalElectricityKwh = day.Where(s => s.PowerInputKilowatt.HasValue)
                        .Sum(s => (double)s.PowerInputKilowatt!.Value * 0.25),
                    TotalHeatOutputKwh = day.Where(s => s.HeatOutputKilowatt.HasValue)
                        .Sum(s => (double)s.HeatOutputKilowatt!.Value * 0.25),

                    AvgOutdoorTemp = AvgDecimal(day, s => s.OutdoorTemperatureCelsius),
                    MinOutdoorTemp = day.Any(s => s.OutdoorTemperatureCelsius.HasValue)
                        ? day.Where(s => s.OutdoorTemperatureCelsius.HasValue)
                            .Select(s => (double)s.OutdoorTemperatureCelsius!.Value).Min()
                        : null,
                    MaxOutdoorTemp = day.Any(s => s.OutdoorTemperatureCelsius.HasValue)
                        ? day.Where(s => s.OutdoorTemperatureCelsius.HasValue)
                            .Select(s => (double)s.OutdoorTemperatureCelsius!.Value).Max()
                        : null,
                    // Fixed flow temp setpoint — only from FixedFlow-mode snapshots
                    AvgFlowTemp = AvgDecimal(fixedFlowSnapshots, s => s.HeatingFlowTemperatureCelsius),
                    AvgRoomTemp = AvgDecimal(day, s => s.RoomTemperatureCelsius),
                    AvgSetpoint = AvgDecimal(day, s => s.HeatingZoneSetpointCelsius),

                    HeatingDutyCyclePercent = day.Count > 0
                        ? day.Count(s => s.HeatingZoneHeatDemand == true) * 100.0 / day.Count
                        : 0,
                    HotWaterDutyCyclePercent = day.Count > 0
                        ? day.Count(s => s.HotWaterZoneHeatDemand == true) * 100.0 / day.Count
                        : 0,

                    FlowTempMode = flowTempModeForDay,
                    WeatherCompMin = wcMinMode.HasValue ? (double)wcMinMode.Value : null,
                    WeatherCompMax = wcMaxMode.HasValue ? (double)wcMaxMode.Value : null,

                    FlowTempAllowableMin = flowTempMinMode.HasValue ? (double)flowTempMinMode.Value : null,
                    FlowTempAllowableMax = flowTempMaxMode.HasValue ? (double)flowTempMaxMode.Value : null,

                    ControllerStateTransitions = stateTransitions,

                    HotWaterRunCount = hwRunCount,
                    HotWaterTotalMinutes = hwTotalSnapshots * 15,
                    AvgHotWaterSetpoint = AvgDecimal(hotWaterSnapshots, s => s.HotWaterZoneSetpointCelsius),
                };
            })
            .ToList();
    }

    public void EnrichAggregatesWithTimeSeries(
        List<DailyAggregateDto> aggregates,
        List<HeatPumpTimeSeriesRecord> timeSeriesRecords,
        List<HeatPumpSnapshot> snapshots)
    {
        // Index snapshots by time for efficient nearest-neighbour lookup
        var snapshotsByTime = snapshots
            .OrderBy(s => s.SnapshotTakenAt)
            .ToList();

        // Deduplicate time series records by StartAt before grouping
        var dedupedRecords = timeSeriesRecords
            .GroupBy(r => r.StartAt)
            .Select(g => g.First())
            .ToList();

        // Group time series records by date
        var tsByDate = dedupedRecords
            .GroupBy(r => DateOnly.FromDateTime(r.StartAt))
            .ToDictionary(g => g.Key, g => g.ToList());

        // Index existing aggregates by date for merging
        var aggByDate = aggregates.ToDictionary(a => a.Date);

        foreach (var (date, records) in tsByDate)
        {
            // Sum energy from time series for the day
            var tsEnergyIn = records
                .Where(r => r.EnergyInputKwh.HasValue)
                .Sum(r => (double)r.EnergyInputKwh!.Value);
            var tsEnergyOut = records
                .Where(r => r.EnergyOutputKwh.HasValue)
                .Sum(r => (double)r.EnergyOutputKwh!.Value);
            var tsOutdoorTemps = records
                .Where(r => r.OutdoorTemperatureCelsius.HasValue)
                .Select(r => (double)r.OutdoorTemperatureCelsius!.Value)
                .ToList();

            // Find flow temp mode and settings from nearest snapshots (within 30 min window)
            var wcValues = new List<(string? flowTempMode, decimal? min, decimal? max, decimal? flowTemp, decimal? flowTempAllowableMin, decimal? flowTempAllowableMax)>();
            foreach (var rec in records)
            {
                var nearest = FindNearestSnapshot(snapshotsByTime, rec.StartAt, TimeSpan.FromMinutes(30));
                if (nearest is not null)
                {
                    wcValues.Add((
                        nearest.FlowTempMode,
                        nearest.WeatherCompensationMinCelsius,
                        nearest.WeatherCompensationMaxCelsius,
                        nearest.HeatingFlowTemperatureCelsius,
                        nearest.HeatingFlowTempAllowableMinCelsius,
                        nearest.HeatingFlowTempAllowableMaxCelsius
                    ));
                }
            }

            // Flow temp mode (most frequent non-null)
            var flowTempModeForDay = wcValues
                .Where(v => v.flowTempMode != null)
                .GroupBy(v => v.flowTempMode!)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            // WC curve range — only from WeatherCompensation-mode snapshots
            var wcMinMode = wcValues
                .Where(v => v.flowTempMode == FlowTempMode.WeatherCompensation && v.min.HasValue)
                .GroupBy(v => v.min!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            var wcMaxMode = wcValues
                .Where(v => v.flowTempMode == FlowTempMode.WeatherCompensation && v.max.HasValue)
                .GroupBy(v => v.max!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            // Fixed flow setpoint — only from FixedFlow-mode snapshots
            var avgFlowTemp = wcValues
                .Where(v => v.flowTempMode == FlowTempMode.FixedFlow && v.flowTemp.HasValue)
                .Select(v => (double)v.flowTemp!.Value)
                .ToList();

            var flowTempAllowMinMode = wcValues
                .Where(v => v.flowTempAllowableMin.HasValue)
                .GroupBy(v => v.flowTempAllowableMin!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            var flowTempAllowMaxMode = wcValues
                .Where(v => v.flowTempAllowableMax.HasValue)
                .GroupBy(v => v.flowTempAllowableMax!.Value)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            if (aggByDate.TryGetValue(date, out var existing))
            {
                // Merge: prefer time series energy totals (more complete hourly data)
                if (tsEnergyIn > 0)
                    existing.TotalElectricityKwh = tsEnergyIn;
                if (tsEnergyOut > 0)
                    existing.TotalHeatOutputKwh = tsEnergyOut;

                // Supplement outdoor temp from time series if snapshot data is missing
                if (!existing.AvgOutdoorTemp.HasValue && tsOutdoorTemps.Count > 0)
                    existing.AvgOutdoorTemp = tsOutdoorTemps.Average();
                if (!existing.MinOutdoorTemp.HasValue && tsOutdoorTemps.Count > 0)
                    existing.MinOutdoorTemp = tsOutdoorTemps.Min();
                if (!existing.MaxOutdoorTemp.HasValue && tsOutdoorTemps.Count > 0)
                    existing.MaxOutdoorTemp = tsOutdoorTemps.Max();

                // Supplement flow temp mode from time-series-correlated snapshots if not already set
                if (existing.FlowTempMode == null && flowTempModeForDay != null)
                    existing.FlowTempMode = flowTempModeForDay;
                if (!existing.WeatherCompMin.HasValue && wcMinMode.HasValue)
                    existing.WeatherCompMin = (double)wcMinMode.Value;
                if (!existing.WeatherCompMax.HasValue && wcMaxMode.HasValue)
                    existing.WeatherCompMax = (double)wcMaxMode.Value;
                if (!existing.AvgFlowTemp.HasValue && avgFlowTemp.Count > 0)
                    existing.AvgFlowTemp = avgFlowTemp.Average();
                if (!existing.FlowTempAllowableMin.HasValue && flowTempAllowMinMode.HasValue)
                    existing.FlowTempAllowableMin = (double)flowTempAllowMinMode.Value;
                if (!existing.FlowTempAllowableMax.HasValue && flowTempAllowMaxMode.HasValue)
                    existing.FlowTempAllowableMax = (double)flowTempAllowMaxMode.Value;

                // Compute COP from time series energy data
                if (tsEnergyIn > 0 && tsEnergyOut > 0)
                    existing.AvgCopHeating ??= tsEnergyOut / tsEnergyIn;
            }
            else
            {
                // Create new aggregate from time series data for dates without snapshots
                var newAgg = new DailyAggregateDto
                {
                    Date = date,
                    SnapshotCount = 0,
                    TotalElectricityKwh = tsEnergyIn,
                    TotalHeatOutputKwh = tsEnergyOut,
                    AvgOutdoorTemp = tsOutdoorTemps.Count > 0 ? tsOutdoorTemps.Average() : null,
                    MinOutdoorTemp = tsOutdoorTemps.Count > 0 ? tsOutdoorTemps.Min() : null,
                    MaxOutdoorTemp = tsOutdoorTemps.Count > 0 ? tsOutdoorTemps.Max() : null,
                    AvgCopHeating = tsEnergyIn > 0 ? tsEnergyOut / tsEnergyIn : null,
                    FlowTempMode = flowTempModeForDay,
                    WeatherCompMin = wcMinMode.HasValue ? (double)wcMinMode.Value : null,
                    WeatherCompMax = wcMaxMode.HasValue ? (double)wcMaxMode.Value : null,
                    AvgFlowTemp = avgFlowTemp.Count > 0 ? avgFlowTemp.Average() : null,
                    FlowTempAllowableMin = flowTempAllowMinMode.HasValue ? (double)flowTempAllowMinMode.Value : null,
                    FlowTempAllowableMax = flowTempAllowMaxMode.HasValue ? (double)flowTempAllowMaxMode.Value : null,
                };

                aggregates.Add(newAgg);
            }
        }

        // Re-sort by date after adding new entries
        aggregates.Sort((a, b) => a.Date.CompareTo(b.Date));
    }

    private static HeatPumpSnapshot? FindNearestSnapshot(List<HeatPumpSnapshot> sortedSnapshots, DateTime target, TimeSpan maxDistance)
    {
        if (sortedSnapshots.Count == 0) return null;

        // Binary search for the insertion point
        var idx = sortedSnapshots.BinarySearch(null!, Comparer<HeatPumpSnapshot>.Create(
            (a, _) => a!.SnapshotTakenAt.CompareTo(target)));

        if (idx < 0) idx = ~idx; // bitwise complement gives the insertion point

        HeatPumpSnapshot? best = null;
        var bestDistance = TimeSpan.MaxValue;

        // Check the element at idx and idx-1 (the two nearest candidates)
        for (var i = Math.Max(0, idx - 1); i <= Math.Min(sortedSnapshots.Count - 1, idx); i++)
        {
            var distance = (sortedSnapshots[i].SnapshotTakenAt - target).Duration();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = sortedSnapshots[i];
            }
        }

        return bestDistance <= maxDistance ? best : null;
    }

    private static double? AvgDecimal(List<HeatPumpSnapshot> snapshots, Func<HeatPumpSnapshot, decimal?> selector)
    {
        var values = snapshots.Where(s => selector(s).HasValue).Select(s => (double)selector(s)!.Value).ToList();
        return values.Count > 0 ? values.Average() : null;
    }
}
