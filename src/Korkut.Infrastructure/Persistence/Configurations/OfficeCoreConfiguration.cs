using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the office-core aggregates (matter/client/court/party/hearing) — split
/// out of KorkutDbContext.OnModelCreating (Clean Architecture layout). Kept together
/// because the tenant-aware composite foreign keys ((office_id, id) principal keys) tie
/// these tables to one another and must be configured as a set.
/// </summary>
public sealed class MatterConfiguration : IEntityTypeConfiguration<MatterEntity>
{
    public void Configure(EntityTypeBuilder<MatterEntity> builder)
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
        builder.HasOne<ClientEntity>()
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

/// <summary>EF mapping for clients.</summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(400);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.HasAlternateKey(x => new { x.OfficeId, x.Id }); // composite FK target for matters
        builder.HasIndex(x => new { x.OfficeId, x.DeletedAt }).HasDatabaseName("ix_clients_office_deleted");
    }
}

/// <summary>EF mapping for courts.</summary>
public sealed class CourtConfiguration : IEntityTypeConfiguration<CourtEntity>
{
    public void Configure(EntityTypeBuilder<CourtEntity> builder)
    {
        builder.ToTable("courts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(400);
        builder.HasIndex(x => x.OfficeId).HasDatabaseName("ix_courts_office");
    }
}

/// <summary>EF mapping for matter parties (tenant-aware composite FK to the matter).</summary>
public sealed class PartyConfiguration : IEntityTypeConfiguration<PartyEntity>
{
    public void Configure(EntityTypeBuilder<PartyEntity> builder)
    {
        builder.ToTable("parties");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Role).HasMaxLength(50);
        builder.Property(x => x.Name).HasMaxLength(400);
        builder.HasOne<MatterEntity>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.MatterId })
            .HasPrincipalKey(m => new { m.OfficeId, m.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_parties_office_matter");
    }
}

/// <summary>EF mapping for matter hearings (tenant-aware composite FK to the matter).</summary>
public sealed class MatterHearingConfiguration : IEntityTypeConfiguration<MatterHearingEntity>
{
    public void Configure(EntityTypeBuilder<MatterHearingEntity> builder)
    {
        builder.ToTable("matter_hearings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasMaxLength(100);
        builder.Property(x => x.Location).HasMaxLength(400);
        builder.Property(x => x.Result).HasMaxLength(1000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.ReminderStatus).HasMaxLength(20);
        builder.HasOne<MatterEntity>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.MatterId })
            .HasPrincipalKey(m => new { m.OfficeId, m.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_matter_hearings_office_matter");
    }
}
