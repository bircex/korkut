using Korkut.Application.Jobs;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// The Korkut EF Core context. Single UUIDv7 identity strategy (no numeric
/// surrogate ids), timestamps stored as UTC <c>timestamptz</c>, enums stored as
/// their string value for readable, stable columns. Every index below is
/// justified by a real query pattern (see docs/migration/data/INDEXES.md).
/// </summary>
public sealed class KorkutDbContext(DbContextOptions<KorkutDbContext> options) : DbContext(options)
{
    public DbSet<OutboxEntity> Outbox => Set<OutboxEntity>();

    public DbSet<InboxEntity> Inbox => Set<InboxEntity>();

    public DbSet<JobAuditEntity> JobAudit => Set<JobAuditEntity>();

    public DbSet<OfficeEntity> Offices => Set<OfficeEntity>();

    public DbSet<MatterEntity> Matters => Set<MatterEntity>();

    public DbSet<MatterHearingEntity> MatterHearings => Set<MatterHearingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("korkut");

        modelBuilder.Entity<OutboxEntity>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();          // app-assigned UUIDv7
            b.Property(x => x.Type).HasMaxLength(200);
            b.Property(x => x.IdempotencyKey).HasMaxLength(200);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.LeaseOwner).HasMaxLength(200);

            // Query: dispatcher leases due, unleased messages ordered by AvailableAt.
            b.HasIndex(x => new { x.Status, x.AvailableAt }).HasDatabaseName("ix_outbox_status_available_at");
            // Query: dedup / idempotency lookups are scoped by office + key.
            b.HasIndex(x => new { x.OfficeId, x.IdempotencyKey }).HasDatabaseName("ix_outbox_office_key");
        });

        modelBuilder.Entity<InboxEntity>(b =>
        {
            b.ToTable("inbox_entries");
            // Composite PK enforces at-most-once per (office, key) — the idempotency guarantee.
            b.HasKey(x => new { x.OfficeId, x.IdempotencyKey });
            b.Property(x => x.IdempotencyKey).HasMaxLength(200);
        });

        modelBuilder.Entity<JobAuditEntity>(b =>
        {
            b.ToTable("job_audit");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Type).HasMaxLength(200);
            b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);

            // Query: operator audit is read per office, newest first.
            b.HasIndex(x => new { x.OfficeId, x.At }).HasDatabaseName("ix_job_audit_office_at");
            // Query: trace all events for a specific message.
            b.HasIndex(x => x.MessageId).HasDatabaseName("ix_job_audit_message");
        });

        modelBuilder.Entity<OfficeEntity>(b =>
        {
            b.ToTable("offices");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Name).HasMaxLength(300);
        });

        modelBuilder.Entity<MatterEntity>(b =>
        {
            b.ToTable("matters");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Title).HasMaxLength(500);
            // Composite alternate key so children can reference (office_id, id) — the
            // tenant-aware FK target that makes cross-office links impossible.
            b.HasAlternateKey(x => new { x.OfficeId, x.Id });
            b.HasIndex(x => x.OfficeId).HasDatabaseName("ix_matters_office");
        });

        modelBuilder.Entity<MatterHearingEntity>(b =>
        {
            b.ToTable("matter_hearings");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            // Tenant-aware composite FK: (office_id, matter_id) -> matters(office_id, id).
            b.HasOne<MatterEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.OfficeId, x.MatterId })
                .HasPrincipalKey(m => new { m.OfficeId, m.Id })
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_matter_hearings_office_matter");
        });

        // snake_case all columns (Postgres convention; matches the Python schema and
        // the hand-written lease SQL). Applied after per-entity config so it wins.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (!char.IsUpper(name[i - 1]) || (i + 1 < name.Length && !char.IsUpper(name[i + 1]))))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
