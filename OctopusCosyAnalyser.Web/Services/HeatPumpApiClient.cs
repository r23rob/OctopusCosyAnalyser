using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.Web.Services;

/// <summary>
/// Typed HTTP client for all heat-pump API calls.
/// Registered in DI and configured with Aspire service discovery base address.
/// </summary>
public class HeatPumpApiClient
{
    private readonly HttpClient _http;

    public HeatPumpApiClient(HttpClient http)
    {
        _http = http;
    }

    // ── Account Settings ─────────────────────────────────────────────

    public async Task<AccountSettingsDto[]> GetSettingsAsync()
        => await _http.GetFromJsonAsync<AccountSettingsDto[]>("/api/settings") ?? [];

    public async Task<AccountSettingsDto?> GetSettingsByAccountAsync(string accountNumber)
        => await _http.GetFromJsonAsync<AccountSettingsDto>($"/api/settings/{accountNumber}");

    public async Task<AccountSettingsDto?> UpsertSettingsAsync(AccountSettingsRequestDto request)
    {
        var response = await _http.PutAsJsonAsync("/api/settings", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AccountSettingsDto>();
    }

    // ── Device Management ────────────────────────────────────────────

    public async Task<HeatPumpDeviceDto[]> GetDevicesAsync()
        => await _http.GetFromJsonAsync<HeatPumpDeviceDto[]>("/api/heatpump/devices") ?? [];

    public async Task<SetupResponseDto?> SetupDeviceAsync(string accountNumber)
    {
        var response = await _http.PostAsJsonAsync($"/api/heatpump/setup?accountNumber={accountNumber}", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SetupResponseDto>();
    }

    // ── Summary (full live data) ─────────────────────────────────────

    public async Task<HeatPumpSummaryDto?> GetSummaryAsync(string deviceId)
        => await _http.GetFromJsonAsync<HeatPumpSummaryDto>($"/api/heatpump/summary/{deviceId}");

    // ── Period Summary (aggregated stats) ─────────────────────────────

    public async Task<PeriodSummaryDto?> GetPeriodSummaryAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-7)).ToString("o");
        var toStr = (to ?? DateTime.UtcNow).ToString("o");
        return await _http.GetFromJsonAsync<PeriodSummaryDto>(
            $"/api/heatpump/period-summary/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
    }

    // ── Snapshots (persisted history from background worker) ─────────

    public async Task<LatestSnapshotDto?> GetLatestSnapshotAsync(string deviceId)
        => await _http.GetFromJsonAsync<LatestSnapshotDto>($"/api/heatpump/snapshots/{deviceId}/latest");

    public async Task<SnapshotsResponseDto?> GetSnapshotsAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-7)).ToString("o");
        var toStr = (to ?? DateTime.UtcNow).ToString("o");
        return await _http.GetFromJsonAsync<SnapshotsResponseDto>(
            $"/api/heatpump/snapshots/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
    }

    // ── Time Series (Octopus API historic data) ──────────────────────

    public async Task<string> GetTimeSeriesRawAsync(string accountNumber, string euid, DateTime from, DateTime to, string? grouping = null)
    {
        var fromStr = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var url = string.IsNullOrEmpty(grouping)
            ? $"/api/heatpump/time-series/{accountNumber}/{euid}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}"
            : $"/api/heatpump/time-series/{accountNumber}/{euid}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}&grouping={grouping}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Fetches time-series data and parses the raw GraphQL response into typed points.
    /// </summary>
    public async Task<TimeSeriesResult> GetTimeSeriesAsync(string accountNumber, string euid, DateTime from, DateTime to, string? grouping = null)
    {
        var raw = await GetTimeSeriesRawAsync(accountNumber, euid, from, to, grouping);
        var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement.GetProperty("data");

        if (root.TryGetProperty("octoHeatPumpTimeSeriesPerformance", out var seriesEl)
            && seriesEl.ValueKind == JsonValueKind.Array)
        {
            var points = new List<TimeSeriesChartPoint>();
            foreach (var item in seriesEl.EnumerateArray())
            {
                var pt = new TimeSeriesChartPoint();

                if (item.TryGetProperty("endAt", out var endAt) && DateTime.TryParse(endAt.GetString(), out var dt))
                    pt.EndAt = dt;

                var energyOut = GetNestedValue(item, "energyOutput");
                var energyIn = GetNestedValue(item, "energyInput");
                var outdoor = GetNestedValue(item, "outdoorTemperature");

                pt.EnergyOutputVal = energyOut ?? 0;
                pt.EnergyInputVal = energyIn ?? 0;
                pt.OutdoorTempVal = outdoor ?? 0;
                pt.Cop = energyIn > 0 ? (energyOut ?? 0) / energyIn.Value : 0;

                points.Add(pt);
            }
            return new TimeSeriesResult { Points = points, Status = TimeSeriesStatus.Ok };
        }
        else if (root.TryGetProperty("octoHeatPumpTimeSeriesPerformance", out var nullEl)
                 && nullEl.ValueKind == JsonValueKind.Null)
        {
            return new TimeSeriesResult { Points = [], Status = TimeSeriesStatus.NoData };
        }

        return new TimeSeriesResult { Points = [], Status = TimeSeriesStatus.UnexpectedFormat };
    }

    private static double? GetNestedValue(JsonElement item, string property)
    {
        if (item.TryGetProperty(property, out var el) && el.TryGetProperty("value", out var val))
        {
            var str = val.GetString();
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
                return result;
        }
        return null;
    }

    // ── Stored Time Series (from DB) ──────────────────────────────────

    public async Task<TimeSeriesResult> GetStoredTimeSeriesAsync(string deviceId, DateTime from, DateTime to)
    {
        var fromStr = from.ToUniversalTime().ToString("o");
        var toStr = to.ToUniversalTime().ToString("o");

        var response = await _http.GetAsync(
            $"/api/heatpump/timeseries/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
        {
            var points = new List<TimeSeriesChartPoint>();
            foreach (var item in records.EnumerateArray())
            {
                if (!item.TryGetProperty("endAt", out var endAt)
                    || !DateTimeOffset.TryParse(endAt.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                    continue;

                var pt = new TimeSeriesChartPoint { EndAt = dto.UtcDateTime };

                if (item.TryGetProperty("energyOutputKwh", out var eoEl) && eoEl.ValueKind == JsonValueKind.Number)
                    pt.EnergyOutputVal = eoEl.GetDouble();

                if (item.TryGetProperty("energyInputKwh", out var eiEl) && eiEl.ValueKind == JsonValueKind.Number)
                    pt.EnergyInputVal = eiEl.GetDouble();

                if (item.TryGetProperty("outdoorTemperatureCelsius", out var otEl) && otEl.ValueKind == JsonValueKind.Number)
                    pt.OutdoorTempVal = otEl.GetDouble();

                pt.Cop = pt.EnergyInputVal > 0 ? pt.EnergyOutputVal / pt.EnergyInputVal : 0;

                points.Add(pt);
            }
            return new TimeSeriesResult { Points = points, Status = points.Count > 0 ? TimeSeriesStatus.Ok : TimeSeriesStatus.NoData };
        }

        return new TimeSeriesResult { Points = [], Status = TimeSeriesStatus.NoData };
    }

    public async Task<SyncResult> SyncTimeSeriesAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddMonths(-12)).ToUniversalTime().ToString("o");
        var toStr = (to ?? DateTime.UtcNow).ToUniversalTime().ToString("o");
        var response = await _http.PostAsync(
            $"/api/heatpump/sync-timeseries/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}", null);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var synced = doc.RootElement.TryGetProperty("synced", out var s) ? s.GetInt32() : 0;
        var skipped = doc.RootElement.TryGetProperty("skipped", out var sk) ? sk.GetInt32() : 0;
        return new SyncResult { Synced = synced, Skipped = skipped };
    }

    // ── Time Ranged (Octopus API aggregated) ─────────────────────────

    public async Task<string> GetTimeRangedRawAsync(string accountNumber, string euid, DateTime from, DateTime to)
    {
        var fromStr = from.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = to.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var response = await _http.GetAsync(
            $"/api/heatpump/time-ranged/{accountNumber}/{euid}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // ── Consumption (smart meter readings) ───────────────────────────

    public async Task<ConsumptionReadingDto[]> GetConsumptionAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-7)).ToString("o");
        var toStr = (to ?? DateTime.UtcNow).ToString("o");
        return await _http.GetFromJsonAsync<ConsumptionReadingDto[]>(
            $"/api/heatpump/consumption/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}") ?? [];
    }

    public async Task SyncConsumptionAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-7)).ToString("o");
        var toStr = (to ?? DateTime.UtcNow).ToString("o");
        var response = await _http.PostAsync(
            $"/api/heatpump/sync/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}", null);
        response.EnsureSuccessStatusCode();
    }

    // ── Controllers at Location (Multi-HP) ──────────────────────────

    public async Task<string> GetControllersAtLocationRawAsync(string accountNumber, int propertyId)
    {
        var response = await _http.GetAsync($"/api/heatpump/controllers-at-location/{accountNumber}/{propertyId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // ── Applicable Rates (Tariff) ─────────────────────────────────

    public async Task<string> GetApplicableRatesRawAsync(string accountNumber, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-1)).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = (to ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var response = await _http.GetAsync(
            $"/api/heatpump/rates/{accountNumber}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    // ── Daily Aggregates ───────────────────────────────────────────

    public async Task<DailyAggregateDto[]> GetDailyAggregatesAsync(string deviceId, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-30)).ToString("o");
        var toStr = (to ?? DateTime.UtcNow).ToString("o");
        var response = await _http.GetFromJsonAsync<JsonDocument>(
            $"/api/heatpump/daily-aggregates/{deviceId}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
        if (response is null) return [];
        var aggregates = response.RootElement.GetProperty("aggregates");
        return JsonSerializer.Deserialize<DailyAggregateDto[]>(aggregates.GetRawText()) ?? [];
    }

    // ── AI Analysis ─────────────────────────────────────────────────

    public async Task<AiAnalysisResponseDto?> GetAiAnalysisAsync(string deviceId, AiAnalysisRequestDto request)
    {
        var response = await _http.PostAsJsonAsync($"/api/heatpump/ai-analysis/{deviceId}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiAnalysisResponseDto>();
    }

    // ── AI Dashboard Summary ──────────────────────────────────────

    public async Task<AiSummaryDto?> GetAiSummaryAsync(string deviceId)
        => await _http.GetFromJsonAsync<AiSummaryDto>($"/api/heatpump/ai-summary/{deviceId}");

    public async Task<AiSummaryDto?> RefreshAiSummaryAsync(string deviceId)
        => await _http.GetFromJsonAsync<AiSummaryDto>($"/api/heatpump/ai-summary/{deviceId}/refresh");

    // ── Cost of Usage ─────────────────────────────────────────────

    public async Task<string> GetCostOfUsageRawAsync(string accountNumber, DateTime? from = null, DateTime? to = null)
    {
        var fromStr = (from ?? DateTime.UtcNow.AddDays(-7)).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");
        var toStr = (to ?? DateTime.UtcNow).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.ffffff+00:00");

        var response = await _http.GetAsync(
            $"/api/heatpump/cost/{accountNumber}?from={Uri.EscapeDataString(fromStr)}&to={Uri.EscapeDataString(toStr)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>View-model for Radzen time-series charts — all numeric properties for chart binding.</summary>
public sealed class TimeSeriesChartPoint
{
    public DateTime EndAt { get; set; }
    public double Cop { get; set; }
    public double EnergyOutputVal { get; set; }
    public double EnergyInputVal { get; set; }
    public double OutdoorTempVal { get; set; }
}

public enum TimeSeriesStatus { Ok, NoData, UnexpectedFormat }

public sealed class TimeSeriesResult
{
    public List<TimeSeriesChartPoint> Points { get; set; } = [];
    public TimeSeriesStatus Status { get; set; }
}

public sealed class SyncResult
{
    public int Synced { get; set; }
    public int Skipped { get; set; }
}

