using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;

namespace OctopusCosyAnalyser.ApiService.Data;

/// <summary>
/// Used by `dotnet ef migrations add` to construct a CosyDbContext at design time without
/// running the full ASP.NET Core host. We supply a placeholder Postgres connection string —
/// the migration tooling never executes against the database, only inspects the model — and
/// the SystemCurrentUserAccessor (UserId always null) so global query filters compile cleanly.
/// At design time we deliberately skip the ISecretProtector wiring so column types in
/// migrations match the underlying string columns (no encryption metadata gets baked in).
/// </summary>
public sealed class CosyDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CosyDbContext>
{
    public CosyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CosyDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=cosydb;Username=postgres;Password=design-time");
        return new CosyDbContext(optionsBuilder.Options, new SystemCurrentUserAccessor(), secretProtector: null);
    }
}
