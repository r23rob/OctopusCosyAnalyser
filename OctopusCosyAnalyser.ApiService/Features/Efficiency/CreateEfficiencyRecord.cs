using OctopusCosyAnalyser.ApiService.Application.Interfaces;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>POST /api/efficiency/records — create a new daily efficiency record.</summary>
public static class CreateEfficiencyRecord
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapPost("/records", HandleAsync)
             .WithName("CreateEfficiencyRecord");

    private static async Task<IResult> HandleAsync(
        HeatPumpEfficiencyRecordRequest request,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        if (await repo.ExistsForDateAsync(request.Date, ct: ct))
            return Results.Conflict($"A record already exists for {request.Date}.");

        var record = EfficiencyMapper.ToEntity(request);
        await repo.AddAsync(record, ct);
        await repo.SaveChangesAsync(ct);
        return Results.Created($"/api/efficiency/records/{record.Id}", EfficiencyMapper.ToDto(record));
    }
}
