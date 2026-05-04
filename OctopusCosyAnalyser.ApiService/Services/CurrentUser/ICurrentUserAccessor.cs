namespace OctopusCosyAnalyser.ApiService.Services.CurrentUser;

/// <summary>
/// Resolves the current authenticated user's id from HttpContext.
/// Returns null when invoked outside an HTTP request (background workers, migrations, run-once jobs).
/// </summary>
public interface ICurrentUserAccessor
{
    /// <summary>
    /// AspNetUsers.Id (string GUID) of the current user, or null when no user is bound.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// True when a user is bound — endpoints that require authentication will already have rejected
    /// unauthenticated requests, but this is useful in shared services.
    /// </summary>
    bool IsAuthenticated { get; }
}
