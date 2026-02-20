using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>GET /api/efficiency/filter — filter records to a specific outdoor temperature range.</summary>
public static class FilterByTemperature
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/filter", HandleAsync)
             .WithName("FilterEfficiencyRecords");

    private static async Task<IResult> HandleAsync(
        decimal minOutdoorC, decimal maxOutdoorC,
        DateOnly? from, DateOnly? to,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var records = await repo.GetRecordsAsync(from, to, ct);
        var dtos = records.Select(EfficiencyMapper.ToDto).ToList();
        return Results.Ok(EfficiencyAnalysis.FilterByTemperatureRange(dtos, minOutdoorC, maxOutdoorC));
    }
}
