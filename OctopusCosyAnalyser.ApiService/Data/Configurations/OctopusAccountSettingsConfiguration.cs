using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class OctopusAccountSettingsConfiguration : IEntityTypeConfiguration<OctopusAccountSettings>
{
    public void Configure(EntityTypeBuilder<OctopusAccountSettings> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.AccountNumber).IsUnique();
        entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ApiKey).IsRequired().HasMaxLength(200);
        entity.Property(e => e.Email).HasMaxLength(255);
        entity.Property(e => e.OctopusPassword).HasMaxLength(200);
        entity.Property(e => e.AnthropicApiKey).HasMaxLength(200);
    }
}
