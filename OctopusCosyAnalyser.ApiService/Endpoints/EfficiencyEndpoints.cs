using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Data;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services;
using OctopusCosyAnalyser.Shared.Models;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

public static class EfficiencyEndpoints
{
    public static void MapEfficiencyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/efficiency");

        // GET /api/efficiency/records — list all records (optionally filtered by date range)
        group.MapGet("/records", async (DateOnly? from, DateOnly? to, CosyDbContext db) =>
        {
            var query = db.HeatPumpEfficiencyRecords.AsQueryable();

            if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
            if (to.HasValue) query = query.Where(r => r.Date <= to.Value);

            var records = await query.OrderBy(r => r.Date).ToListAsync();
            return Results.Ok(records.Select(EfficiencyCalculationService.ToDto).ToList());
        }).WithName("GetEfficiencyRecords");

        // GET /api/efficiency/records/{id}
        group.MapGet("/records/{id:int}", async (int id, CosyDbContext db) =>
        {
            var record = await db.HeatPumpEfficiencyRecords.FindAsync(id);
            return record is null
                ? Results.NotFound()
                : Results.Ok(EfficiencyCalculationService.ToDto(record));
        }).WithName("GetEfficiencyRecord");

        // POST /api/efficiency/records — create a new daily record
        group.MapPost("/records", async (HeatPumpEfficiencyRecordRequest request, CosyDbContext db) =>
        {
            if (await db.HeatPumpEfficiencyRecords.AnyAsync(r => r.Date == request.Date))
                return Results.Conflict($"A record already exists for {request.Date}.");

            var record = MapToEntity(request);
            db.HeatPumpEfficiencyRecords.Add(record);
            await db.SaveChangesAsync();
            return Results.Created($"/api/efficiency/records/{record.Id}", EfficiencyCalculationService.ToDto(record));
        }).WithName("CreateEfficiencyRecord");

        // PUT /api/efficiency/records/{id} — update an existing record
        group.MapPut("/records/{id:int}", async (int id, HeatPumpEfficiencyRecordRequest request, CosyDbContext db) =>
        {
            var record = await db.HeatPumpEfficiencyRecords.FindAsync(id);
            if (record is null)
                return Results.NotFound();

            // If the date changed, check uniqueness
            if (record.Date != request.Date && await db.HeatPumpEfficiencyRecords.AnyAsync(r => r.Date == request.Date && r.Id != id))
                return Results.Conflict($"A record already exists for {request.Date}.");

            ApplyUpdate(record, request);
            await db.SaveChangesAsync();
            return Results.Ok(EfficiencyCalculationService.ToDto(record));
        }).WithName("UpdateEfficiencyRecord");

        // DELETE /api/efficiency/records/{id}
        group.MapDelete("/records/{id:int}", async (int id, CosyDbContext db) =>
        {
            var record = await db.HeatPumpEfficiencyRecords.FindAsync(id);
            if (record is null) return Results.NotFound();
            db.HeatPumpEfficiencyRecords.Remove(record);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).WithName("DeleteEfficiencyRecord");

        // GET /api/efficiency/comparison — before vs after comparison
        group.MapGet("/comparison", async (DateOnly? from, DateOnly? to, CosyDbContext db) =>
        {
            var query = db.HeatPumpEfficiencyRecords.AsQueryable();
            if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
            if (to.HasValue) query = query.Where(r => r.Date <= to.Value);

            var records = (await query.OrderBy(r => r.Date).ToListAsync())
                .Select(EfficiencyCalculationService.ToDto)
                .ToList();

            var baseline = records.Where(r => !r.ChangeActive).ToList();
            var change = records.Where(r => r.ChangeActive).ToList();

            return Results.Ok(EfficiencyAnalysisService.Compare(baseline, change));
        }).WithName("GetEfficiencyComparison");

        // GET /api/efficiency/groups — records grouped by ChangeDescription
        group.MapGet("/groups", async (DateOnly? from, DateOnly? to, CosyDbContext db) =>
        {
            var query = db.HeatPumpEfficiencyRecords.AsQueryable();
            if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
            if (to.HasValue) query = query.Where(r => r.Date <= to.Value);

            var records = (await query.OrderBy(r => r.Date).ToListAsync())
                .Select(EfficiencyCalculationService.ToDto)
                .ToList();

            return Results.Ok(EfficiencyAnalysisService.GroupByChange(records));
        }).WithName("GetEfficiencyGroups");

        // GET /api/efficiency/filter — filter by outdoor temperature range
        group.MapGet("/filter", async (decimal minOutdoorC, decimal maxOutdoorC, DateOnly? from, DateOnly? to, CosyDbContext db) =>
        {
            var query = db.HeatPumpEfficiencyRecords.AsQueryable();
            if (from.HasValue) query = query.Where(r => r.Date >= from.Value);
            if (to.HasValue) query = query.Where(r => r.Date <= to.Value);

            var records = (await query.OrderBy(r => r.Date).ToListAsync())
                .Select(EfficiencyCalculationService.ToDto)
                .ToList();

            return Results.Ok(EfficiencyAnalysisService.FilterByTemperatureRange(records, minOutdoorC, maxOutdoorC));
        }).WithName("FilterEfficiencyRecords");
    }

    private static HeatPumpEfficiencyRecord MapToEntity(HeatPumpEfficiencyRecordRequest request)
    {
        var now = DateTime.UtcNow;
        return new HeatPumpEfficiencyRecord
        {
            Date = request.Date,
            ElectricityKWh = request.ElectricityKWh,
            OutdoorAvgC = request.OutdoorAvgC,
            OutdoorHighC = request.OutdoorHighC,
            OutdoorLowC = request.OutdoorLowC,
            IndoorAvgC = request.IndoorAvgC,
            ComfortScore = request.ComfortScore,
            ChangeActive = request.ChangeActive,
            ChangeDescription = request.ChangeDescription,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void ApplyUpdate(HeatPumpEfficiencyRecord record, HeatPumpEfficiencyRecordRequest request)
    {
        record.Date = request.Date;
        record.ElectricityKWh = request.ElectricityKWh;
        record.OutdoorAvgC = request.OutdoorAvgC;
        record.OutdoorHighC = request.OutdoorHighC;
        record.OutdoorLowC = request.OutdoorLowC;
        record.IndoorAvgC = request.IndoorAvgC;
        record.ComfortScore = request.ComfortScore;
        record.ChangeActive = request.ChangeActive;
        record.ChangeDescription = request.ChangeDescription;
        record.Notes = request.Notes;
        record.UpdatedAt = DateTime.UtcNow;
    }
}
