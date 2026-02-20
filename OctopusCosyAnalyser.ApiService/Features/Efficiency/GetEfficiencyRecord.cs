using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>GET /api/efficiency/records/{id} — get a single record by id.</summary>
public static class GetEfficiencyRecord
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapGet("/records/{id:int}", HandleAsync)
             .WithName("GetEfficiencyRecord");

    private static async Task<IResult> HandleAsync(
        int id,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var record = await repo.GetByIdAsync(id, ct);
        return record is null ? Results.NotFound() : Results.Ok(EfficiencyMapper.ToDto(record));
    }
}
