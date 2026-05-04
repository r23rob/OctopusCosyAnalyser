using System.Linq.Expressions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OctopusCosyAnalyser.ApiService.Models;
using OctopusCosyAnalyser.ApiService.Services.CurrentUser;
using OctopusCosyAnalyser.ApiService.Services.SecretProtection;

namespace OctopusCosyAnalyser.ApiService.Data;

public class CosyDbContext : IdentityDbContext<ApplicationUser>, IDataProtectionKeyContext
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ISecretProtector? _secretProtector;

    public CosyDbContext(
        DbContextOptions<CosyDbContext> options,
        ICurrentUserAccessor currentUser,
        ISecretProtector? secretProtector = null)
        : base(options)
    {
        _currentUser = currentUser;
        _secretProtector = secretProtector;
    }

    public DbSet<HeatPumpDevice> HeatPumpDevices { get; set; } = null!;
    public DbSet<ConsumptionReading> ConsumptionReadings { get; set; } = null!;
    public DbSet<OctopusAccountSettings> OctopusAccountSettings { get; set; } = null!;
    public DbSet<HeatPumpSnapshot> HeatPumpSnapshots { get; set; } = null!;
    public DbSet<HeatPumpTimeSeriesRecord> HeatPumpTimeSeriesRecords { get; set; } = null!;
    public DbSet<DailyCostRecord> DailyCostRecords { get; set; } = null!;
    public DbSet<TariffRate> TariffRates { get; set; } = null!;
    public DbSet<EnergyInterval> EnergyIntervals { get; set; } = null!;

    // ASP.NET Core Data Protection key ring (encrypts auth cookies + secrets at rest).
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CosyDbContext).Assembly);

        // Apply per-tenant global query filter to every IOwnedEntity in the model.
        // When _currentUser.UserId is null (background workers, migrations), the filter
        // resolves to false → workers must call IgnoreQueryFilters() explicitly.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IOwnedEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var ownerProp = Expression.Property(parameter, nameof(IOwnedEntity.OwnerId));
                var currentUserExpr = Expression.Property(
                    Expression.Constant(this), nameof(CurrentUserId));
                var body = Expression.Equal(ownerProp, currentUserExpr);
                var lambda = Expression.Lambda(body, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }

        // Tenancy uniqueness — a user can register the same Octopus account number that
        // someone else has registered, but their settings rows must be distinct.
        modelBuilder.Entity<OctopusAccountSettings>()
            .HasIndex(s => new { s.OwnerId, s.AccountNumber })
            .IsUnique();

        modelBuilder.Entity<HeatPumpDevice>()
            .HasIndex(d => new { d.OwnerId, d.DeviceId })
            .IsUnique();

        // Encrypt secrets at rest via a value converter — DB stores ciphertext, code sees plaintext.
        // The converter is bound to the protector singleton at model-build time.
        if (_secretProtector is not null)
        {
            var protector = _secretProtector;
            var nonNullConverter = new ValueConverter<string, string>(
                v => protector.Protect(v) ?? string.Empty,
                v => protector.Unprotect(v) ?? string.Empty);
            var nullableConverter = new ValueConverter<string?, string?>(
                v => protector.Protect(v),
                v => protector.Unprotect(v));

            var settingsBuilder = modelBuilder.Entity<OctopusAccountSettings>();
            settingsBuilder.Property(s => s.ApiKey).HasConversion(nonNullConverter);
            settingsBuilder.Property(s => s.OctopusPassword).HasConversion(nullableConverter);
            settingsBuilder.Property(s => s.AnthropicApiKey).HasConversion(nullableConverter);
        }
    }

    /// <summary>
    /// Exposed as a property so EF can capture it in the global query filter expression
    /// at translation time and re-evaluate per query.
    /// </summary>
    private string? CurrentUserId => _currentUser.UserId;

    public override int SaveChanges()
    {
        StampOwnerOnInsert();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampOwnerOnInsert();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwnerOnInsert();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// On insert, stamp OwnerId from the current user when the caller hasn't already set it.
    /// Workers that snapshot data on behalf of a device must set OwnerId explicitly to the
    /// device's owner — they do not have a user context.
    /// </summary>
    private void StampOwnerOnInsert()
    {
        var currentUserId = _currentUser.UserId;
        if (currentUserId is null)
            return;

        foreach (var entry in ChangeTracker.Entries<IOwnedEntity>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.OwnerId))
            {
                entry.Entity.OwnerId = currentUserId;
            }
        }
    }
}
