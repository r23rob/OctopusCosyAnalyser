using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Services;

public interface IHeatPumpDataService
{
    List<DailyAggregateDto> ComputeDailyAggregates(List<HeatPumpSnapshot> snapshots);

    void EnrichAggregatesWithTimeSeries(
        List<DailyAggregateDto> aggregates,
        List<HeatPumpTimeSeriesRecord> timeSeriesRecords,
        List<HeatPumpSnapshot> snapshots);
}
