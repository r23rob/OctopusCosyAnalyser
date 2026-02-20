using OctopusCosyAnalyser.ApiService.Application.Interfaces;

namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>DELETE /api/efficiency/records/{id} — remove a daily record.</summary>
public static class DeleteEfficiencyRecord
{
    public static void Register(RouteGroupBuilder group) =>
        group.MapDelete("/records/{id:int}", HandleAsync)
             .WithName("DeleteEfficiencyRecord");

    private static async Task<IResult> HandleAsync(
        int id,
        IEfficiencyRepository repo,
        CancellationToken ct)
    {
        var record = await repo.GetByIdAsync(id, ct);
        if (record is null)
            return Results.NotFound();

        await repo.DeleteAsync(record, ct);
        await repo.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
