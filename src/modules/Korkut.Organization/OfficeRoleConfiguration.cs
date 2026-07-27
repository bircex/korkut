using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization;

/// <summary>EF mapping for office-defined roles (table/columns/index unchanged).</summary>
public sealed class OfficeRoleConfiguration : IEntityTypeConfiguration<OfficeRole>
{
    public void Configure(EntityTypeBuilder<OfficeRole> builder)
    {
        builder.ToTable("office_custom_roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Key).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.Property(x => x.Permissions).HasColumnType("text[]");
        builder.HasIndex(x => new { x.OfficeId, x.Key }).IsUnique().HasDatabaseName("ux_custom_roles_office_key");
    }
}
