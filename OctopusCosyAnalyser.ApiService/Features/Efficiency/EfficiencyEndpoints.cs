namespace OctopusCosyAnalyser.ApiService.Features.Efficiency;

/// <summary>Wires all Efficiency feature operations onto the /api/efficiency route group.</summary>
public static class EfficiencyEndpoints
{
    public static IEndpointRouteBuilder MapEfficiencyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/efficiency");

        GetEfficiencyRecords.Register(group);
        GetEfficiencyRecord.Register(group);
        CreateEfficiencyRecord.Register(group);
        UpdateEfficiencyRecord.Register(group);
        DeleteEfficiencyRecord.Register(group);
        ComparePeriods.Register(group);
        GetEfficiencyGroups.Register(group);
        FilterByTemperature.Register(group);

        return routes;
    }
}
