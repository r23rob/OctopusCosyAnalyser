namespace OctopusCosyAnalyser.ApiService.Models;

/// <summary>
/// Marks an entity as belonging to a tenant (an ApplicationUser).
/// CosyDbContext applies a global query filter that scopes reads to the current
/// user, and stamps OwnerId on insert via SaveChanges.
/// </summary>
public interface IOwnedEntity
{
    string? OwnerId { get; set; }
}
