using OctopusCosyAnalyser.ApiService.Application.Efficiency;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class EfficiencyEndpoints
{
    public static void MapEfficiencyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/efficiency");

        // GET /api/efficiency/records
        group.MapGet("/records", async (DateOnly? from, DateOnly? to, GetEfficiencyRecordsHandler handler) =>
            Results.Ok(await handler.HandleAsync(new GetEfficiencyRecordsQuery(from, to)))
        ).WithName("GetEfficiencyRecords");

        // GET /api/efficiency/records/{id}
        group.MapGet("/records/{id:int}", async (int id, GetEfficiencyRecordHandler handler) =>
        {
            var record = await handler.HandleAsync(new GetEfficiencyRecordQuery(id));
            return record is null ? Results.NotFound() : Results.Ok(record);
        }).WithName("GetEfficiencyRecord");

        // POST /api/efficiency/records
        group.MapPost("/records", async (HeatPumpEfficiencyRecordRequest request, CreateEfficiencyRecordHandler handler) =>
        {
            var result = await handler.HandleAsync(new CreateEfficiencyRecordCommand(request));
            if (result.Conflict) return Results.Conflict($"A record already exists for {request.Date}.");
            return Results.Created($"/api/efficiency/records/{result.Record!.Id}", result.Record);
        }).WithName("CreateEfficiencyRecord");

        // PUT /api/efficiency/records/{id}
        group.MapPut("/records/{id:int}", async (int id, HeatPumpEfficiencyRecordRequest request, UpdateEfficiencyRecordHandler handler) =>
        {
            var result = await handler.HandleAsync(new UpdateEfficiencyRecordCommand(id, request));
            if (result.NotFound) return Results.NotFound();
            if (result.Conflict) return Results.Conflict($"A record already exists for {request.Date}.");
            return Results.Ok(result.Record);
        }).WithName("UpdateEfficiencyRecord");

        // DELETE /api/efficiency/records/{id}
        group.MapDelete("/records/{id:int}", async (int id, DeleteEfficiencyRecordHandler handler) =>
        {
            var result = await handler.HandleAsync(new DeleteEfficiencyRecordCommand(id));
            return result.NotFound ? Results.NotFound() : Results.NoContent();
        }).WithName("DeleteEfficiencyRecord");

        // GET /api/efficiency/comparison
        group.MapGet("/comparison", async (DateOnly? from, DateOnly? to, ComparePeriodHandler handler) =>
            Results.Ok(await handler.HandleAsync(new ComparePeriodQuery(from, to)))
        ).WithName("GetEfficiencyComparison");

        // GET /api/efficiency/groups
        group.MapGet("/groups", async (DateOnly? from, DateOnly? to, GetEfficiencyGroupsHandler handler) =>
            Results.Ok(await handler.HandleAsync(new GetEfficiencyGroupsQuery(from, to)))
        ).WithName("GetEfficiencyGroups");

        // GET /api/efficiency/filter
        group.MapGet("/filter", async (decimal minOutdoorC, decimal maxOutdoorC, DateOnly? from, DateOnly? to, FilterEfficiencyByTempHandler handler) =>
            Results.Ok(await handler.HandleAsync(new FilterEfficiencyByTempQuery(minOutdoorC, maxOutdoorC, from, to)))
        ).WithName("FilterEfficiencyRecords");
    }
}
