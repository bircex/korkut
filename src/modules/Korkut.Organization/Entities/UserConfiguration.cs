using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization.Entities;

/// <summary>EF mapping for office users (table/columns/indexes unchanged).</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.FullName).HasMaxLength(200);
        builder.Property(x => x.BarRegistryNo).HasMaxLength(50);
        builder.Property(x => x.PasswordHash).HasMaxLength(400);
        builder.Property(x => x.Role).HasMaxLength(50);
        builder.Property(x => x.GoogleSub).HasMaxLength(255);
        builder.Ignore(x => x.IsBound);
        builder.Ignore(x => x.IsActive);
        // Email is unique within an office (tenant-scoped identity).
        builder.HasIndex(x => new { x.OfficeId, x.Email }).IsUnique().HasDatabaseName("ux_users_office_email");
        // A Google identity links to exactly one account (globally unique subject).
        builder.HasIndex(x => x.GoogleSub).IsUnique().HasDatabaseName("ux_users_google_sub");
    }
}
