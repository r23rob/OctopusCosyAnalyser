using System.Net.Http.Json;
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
    {
        var response = await _http.GetAsync("/api/settings");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength == 0) return [];
        return await response.Content.ReadFromJsonAsync<AccountSettingsDto[]>() ?? [];
    }

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
    {
        var response = await _http.GetAsync("/api/heatpump/devices");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength == 0) return [];
        return await response.Content.ReadFromJsonAsync<HeatPumpDeviceDto[]>() ?? [];
    }

    public async Task<SetupResponseDto?> SetupDeviceAsync(string accountNumber)
    {
        var response = await _http.PostAsJsonAsync($"/api/heatpump/setup?accountNumber={accountNumber}", new { });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SetupResponseDto>();
    }

    // ── Summary (full live data) ─────────────────────────────────────

    public async Task<HeatPumpSummaryDto?> GetSummaryAsync(string deviceId)
        => await _http.GetFromJsonAsync<HeatPumpSummaryDto>($"/api/heatpump/summary/{deviceId}");

    // ── Snapshots (persisted history from background worker) ─────────

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

    // ── Tado Settings ─────────────────────────────────────────────────

    public async Task<TadoSettingsDto?> GetTadoSettingsAsync()
    {
        var response = await _http.GetAsync("/api/tado/settings");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content) || content == "null") return null;
        return System.Text.Json.JsonSerializer.Deserialize<TadoSettingsDto>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<TadoSettingsDto?> UpsertTadoSettingsAsync(TadoSettingsRequestDto request)
    {
        var response = await _http.PutAsJsonAsync("/api/tado/settings", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TadoSettingsDto>();
    }

    // ── Tado Homes ────────────────────────────────────────────────────

    public async Task<TadoHomeDto[]> GetTadoHomesAsync()
        => await _http.GetFromJsonAsync<TadoHomeDto[]>("/api/tado/homes") ?? [];

    // ── Tado Zones ────────────────────────────────────────────────────

    public async Task<TadoZoneDto[]> GetTadoZonesAsync()
        => await _http.GetFromJsonAsync<TadoZoneDto[]>("/api/tado/zones") ?? [];
}

