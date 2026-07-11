using OctopusCosyAnalyser.ApiService.Services;

namespace OctopusCosyAnalyser.ApiService.Endpoints;

/// <summary>
/// Endpoint filter that returns 503 for endpoints requiring the database
/// when the API is running in lite mode (no PostgreSQL connection configured).
/// </summary>
public sealed class RequireDatabaseFilter : IEndpointFilter
{
    private static readonly object UnavailableBody = new
    {
        available = false,
        reason = "Database not configured — historical data requires the full deployment"
    };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var features = context.HttpContext.RequestServices.GetRequiredService<FeatureAvailability>();
        if (!features.DatabaseAvailable)
            return Results.Json(UnavailableBody, statusCode: StatusCodes.Status503ServiceUnavailable);

        return await next(context);
    }
}

public static class EndpointFilterExtensions
{
    /// <summary>
    /// Marks this endpoint as requiring the database. Returns 503 in lite mode.
    /// </summary>
    public static RouteHandlerBuilder RequireDatabase(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<RequireDatabaseFilter>();
}
