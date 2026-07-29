using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Organization.Entities;

/// <summary>EF mapping for office invitations (table/columns/indexes unchanged).</summary>
public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> builder)
    {
        builder.ToTable("invites");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Role).HasMaxLength(20);
        builder.Property(x => x.TokenHash).HasMaxLength(64);
        builder.Ignore(x => x.IsPending);
        builder.HasIndex(x => x.TokenHash).HasDatabaseName("ix_invites_token_hash");
        builder.HasIndex(x => new { x.OfficeId, x.Email }).HasDatabaseName("ix_invites_office_email");
    }
}
