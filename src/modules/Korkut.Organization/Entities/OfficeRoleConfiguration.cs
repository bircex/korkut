using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization.Entities;

/// <summary>EF mapping for the office's roles.</summary>
public sealed class OfficeRoleConfiguration : IEntityTypeConfiguration<OfficeRole>
{
    public void Configure(EntityTypeBuilder<OfficeRole> builder)
    {
        builder.ToTable("office_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Permissions).HasColumnType("text[]");
        builder.HasIndex(x => new { x.OfficeId, x.Key }).IsUnique().HasDatabaseName("ux_office_roles_office_key");
    }
}
