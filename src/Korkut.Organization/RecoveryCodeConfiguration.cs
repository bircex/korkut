using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization;

/// <summary>EF mapping for TOTP recovery codes (table/columns/index unchanged).</summary>
public sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("recovery_codes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CodeHash).HasMaxLength(100);
        builder.HasIndex(x => new { x.OfficeId, x.UserId }).HasDatabaseName("ix_recovery_codes_office_user");
    }
}
