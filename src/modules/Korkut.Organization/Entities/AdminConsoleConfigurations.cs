using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization.Entities;

/// <summary>EF mappings for the platform-admin console tables (cross-tenant, no RLS; the
/// AddAdminConsole migration grants the app role narrow privileges).</summary>
public sealed class AdminTotpConfiguration : IEntityTypeConfiguration<AdminTotp>
{
    public void Configure(EntityTypeBuilder<AdminTotp> builder)
    {
        builder.ToTable("admin_totp");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.EncryptedSecret).HasMaxLength(600);
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_admin_totp_email");
    }
}

public sealed class AdminRecoveryCodeConfiguration : IEntityTypeConfiguration<AdminRecoveryCode>
{
    public void Configure(EntityTypeBuilder<AdminRecoveryCode> builder)
    {
        builder.ToTable("admin_recovery_codes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.CodeHash).HasMaxLength(200);
        builder.HasIndex(x => new { x.Email, x.CodeHash }).HasDatabaseName("ix_admin_recovery_email_hash");
    }
}

public sealed class AdminOfficeModelConfiguration : IEntityTypeConfiguration<AdminOfficeModel>
{
    public void Configure(EntityTypeBuilder<AdminOfficeModel> builder)
    {
        builder.ToTable("admin_office_models");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ModelKey).HasMaxLength(100);
        builder.HasIndex(x => x.OfficeId).IsUnique().HasDatabaseName("ux_admin_office_models_office");
    }
}
