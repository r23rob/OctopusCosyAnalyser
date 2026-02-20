using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>
/// GET /api/efficiency/comparison — compares baseline (ChangeActive=false) vs change period (ChangeActive=true),
/// using HDD-normalised efficiency to determine whether the change improved performance.
/// </summary>
public static class ComparePeriods
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/comparison", HandleAsync)
             .WithName("GetEfficiencyComparison");

    private static async Task<IResult> HandleAsync(
        DateOnly? from, DateOnly? to,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var records = await repo.GetRecordsAsync(from, to, ct);
        var dtos = records.Select(EfficiencyMapper.ToDto).ToList();
        var baseline = dtos.Where(r => !r.ChangeActive).ToList();
        var change = dtos.Where(r => r.ChangeActive).ToList();
        return Results.Ok(EfficiencyAnalysis.Compare(baseline, change));
    }
}
