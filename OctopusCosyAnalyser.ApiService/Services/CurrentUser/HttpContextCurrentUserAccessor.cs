namespace OctopusCosyAnalyser.ApiService.Services.CurrentUser;

public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    public const string FixedUserId = "rob";

    public string? UserId => FixedUserId;

    public bool IsAuthenticated => true;
}

/// <summary>
/// Used by background workers and run-once jobs that have no HttpContext.
/// With UserId null, CosyDbContext's global query filter short-circuits to false
/// (the filter is `CurrentUserId != null && OwnerId == CurrentUserId`), so any
/// owned-entity read goes through zero results unless the worker calls
/// IgnoreQueryFilters() — which all workers do explicitly when iterating across tenants.
/// </summary>
public sealed class SystemCurrentUserAccessor : ICurrentUserAccessor
{
    public string? UserId => null;
    public bool IsAuthenticated => false;
}
