using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>GET /api/efficiency/groups — records grouped by ChangeDescription with per-group summaries.</summary>
public static class GetEfficiencyGroups
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/groups", HandleAsync)
             .WithName("GetEfficiencyGroups");

    private static async Task<IResult> HandleAsync(
        DateOnly? from, DateOnly? to,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var records = await repo.GetRecordsAsync(from, to, ct);
        var dtos = records.Select(EfficiencyMapper.ToDto).ToList();
        return Results.Ok(EfficiencyAnalysis.GroupByChange(dtos));
    }
}
