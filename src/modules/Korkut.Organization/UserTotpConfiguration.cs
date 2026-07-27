using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization;

/// <summary>EF mapping for TOTP enrollments (table/columns/index unchanged).</summary>
public sealed class UserTotpConfiguration : IEntityTypeConfiguration<UserTotp>
{
    public void Configure(EntityTypeBuilder<UserTotp> builder)
    {
        builder.ToTable("user_totp");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EncryptedSecret).HasMaxLength(500);
        // One TOTP enrollment per user.
        builder.HasIndex(x => new { x.OfficeId, x.UserId }).IsUnique().HasDatabaseName("ux_user_totp_office_user");
    }
}
