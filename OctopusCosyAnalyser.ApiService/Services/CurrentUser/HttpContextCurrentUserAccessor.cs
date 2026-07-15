namespace OctopusCosyAnalyser.ApiService.Services.CurrentUser;

public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    public const string FixedUserId = "rob";

    public string? UserId => FixedUserId;

    public bool IsAuthenticated => true;
}

/// <summary>
/// Used by background workers and run-once jobs that have no HttpContext.
/// With UserId null, workers must pass an explicit ownerId when querying
/// ICosyDataStore — they iterate across all owners from the active device registry.
/// </summary>
public sealed class SystemCurrentUserAccessor : ICurrentUserAccessor
{
    public string? UserId => null;
    public bool IsAuthenticated => false;
}
