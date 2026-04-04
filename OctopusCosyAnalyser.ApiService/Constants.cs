namespace OctopusCosyAnalyser.ApiService;

public static class Constants
{
    // Snapshot interval
    public const int SnapshotIntervalMinutes = 15;
    public const double SnapshotIntervalHours = 0.25; // 15 min = 0.25 hours (kW-to-kWh conversion)

    // Token cache
    public const int TokenExpiryMinutes = 55;

    // Pagination
    public const int DefaultPageSize = 100;
    public const int MaxConsumptionPageSize = 25000;

    // Date formatting for Octopus API
    public const string OctopusDateFormat = "yyyy-MM-ddTHH:mm:ss.ffffff+00:00";
    public const string OctopusDateFormatSimple = "yyyy-MM-ddTHH:mm:ssZ";

    // Sync limits
    public const int MaxSyncRangeDays = 400;
    public const int MaxAutoSyncDays = 90;
    public const int MaxAggregateSpanDays = 366;
    public const int MaxAnalysisRangeDays = 365;
    public const int TimeSeriesChunkDays = 60;

    // Coverage thresholds
    public const double MinTimeSeriesCoveragePercent = 0.5;

    // Nearest snapshot window for time series correlation
    public const int NearestSnapshotWindowMinutes = 30;
}
