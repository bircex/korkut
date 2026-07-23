using Korkut.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Auth;

/// <summary>EF mapping for password-reset tokens (table/columns/index unchanged).</summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TokenHash).HasMaxLength(100);
        // Reset lookups are by (office, token hash).
        builder.HasIndex(x => new { x.OfficeId, x.TokenHash }).HasDatabaseName("ix_reset_tokens_office_hash");
    }
}
