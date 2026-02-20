using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>PUT /api/efficiency/records/{id} — update an existing daily record.</summary>
public static class UpdateEfficiencyRecord
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapPut("/records/{id:int}", HandleAsync)
             .WithName("UpdateEfficiencyRecord");

    private static async Task<IResult> HandleAsync(
        int id,
        HeatPumpEfficiencyRecordRequest request,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var record = await repo.GetByIdAsync(id, ct);
        if (record is null)
            return Results.NotFound();

        if (record.Date != request.Date && await repo.ExistsForDateAsync(request.Date, excludeId: id, ct: ct))
            return Results.Conflict($"A record already exists for {request.Date}.");

        EfficiencyMapper.ApplyUpdate(record, request);
        await repo.SaveChangesAsync(ct);
        return Results.Ok(EfficiencyMapper.ToDto(record));
    }
}
