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
/// CosyDbContext sees a null UserId and skips the query filter — workers must call
/// IgnoreQueryFilters() explicitly when reading owned data they want unscoped.
/// </summary>
public sealed class SystemCurrentUserAccessor : ICurrentUserAccessor
{
    public string? UserId => null;
    public bool IsAuthenticated => false;
}
