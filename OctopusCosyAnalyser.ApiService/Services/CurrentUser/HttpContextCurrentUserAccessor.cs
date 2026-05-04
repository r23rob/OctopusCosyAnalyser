using System.Security.Claims;

namespace OctopusCosyAnalyser.ApiService.Services.CurrentUser;

public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
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
