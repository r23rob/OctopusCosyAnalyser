using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>GET /api/efficiency/records — list records, optionally filtered by date range.</summary>
public static class GetEfficiencyRecords
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/records", HandleAsync)
             .WithName("GetEfficiencyRecords");

    private static async Task<IResult> HandleAsync(
        DateOnly? from, DateOnly? to,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var records = await repo.GetRecordsAsync(from, to, ct);
        return Results.Ok(records.Select(EfficiencyMapper.ToDto));
    }
}
