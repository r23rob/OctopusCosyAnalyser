namespace OctopusCosyAnalyser.ApiService.Features.Tado;

/// <summary>Wires all Tado feature operations onto the /api/tado route group.</summary>
public static class TadoEndpoints
{
    public static IEndpointRouteBuilder MapTadoEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/tado");

        GetTadoSettings.Register(group);
        UpsertTadoSettings.Register(group);
        GetTadoHomes.Register(group);
        GetTadoZones.Register(group);

        return routes;
    }
}
