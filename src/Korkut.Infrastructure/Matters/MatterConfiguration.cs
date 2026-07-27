using Korkut.Clients;
using Korkut.Domain.Matters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Matters;

/// <summary>EF mapping for the <see cref="Matter"/> aggregate (table/columns unchanged).</summary>
public sealed class MatterConfiguration : IEntityTypeConfiguration<Matter>
{
    public void Configure(EntityTypeBuilder<Matter> builder)
    {
        builder.ToTable("matters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(500);
        // Composite alternate key so children can reference (office_id, id) — the
        // tenant-aware FK target that makes cross-office links impossible.
        builder.HasAlternateKey(x => new { x.OfficeId, x.Id });
        builder.HasIndex(x => x.OfficeId).HasDatabaseName("ix_matters_office");
        // Optimistic concurrency via Postgres' system xmin column.
        builder.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
        // Tenant-aware composite FK to the client (optional).
        builder.HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.ClientId })
            .HasPrincipalKey(c => new { c.OfficeId, c.Id })
            .OnDelete(DeleteBehavior.SetNull);
        builder.Property(x => x.Status).HasMaxLength(30);
        builder.Property(x => x.FileNo).HasMaxLength(100);
        builder.Property(x => x.CaseType).HasMaxLength(100);
        builder.Property(x => x.CourtNameSnapshot).HasMaxLength(400);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_matters_office_status");
    }
}
