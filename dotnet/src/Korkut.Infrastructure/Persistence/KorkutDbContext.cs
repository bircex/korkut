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

    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();

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
            // Optimistic concurrency via Postgres' system xmin column — a stale update
            // raises a concurrency conflict instead of silently overwriting (KDM-014).
            b.Property<uint>("xmin").HasColumnName("xmin").IsRowVersion();
        });

        modelBuilder.Entity<AuditEventEntity>(b =>
        {
            b.ToTable("audit_events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.EntityType).HasMaxLength(100);
            b.Property(x => x.SessionId).HasMaxLength(200);
            b.Property(x => x.CorrelationId).HasMaxLength(200);
            b.HasIndex(x => new { x.OfficeId, x.At }).HasDatabaseName("ix_audit_events_office_at");
            b.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_audit_events_entity");
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

        modelBuilder.Entity<UserEntity>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.PasswordHash).HasMaxLength(400);
            b.Property(x => x.GoogleSub).HasMaxLength(255);
            // Email is unique within an office (tenant-scoped identity).
            b.HasIndex(x => new { x.OfficeId, x.Email }).IsUnique().HasDatabaseName("ux_users_office_email");
            // A Google identity links to exactly one account (globally unique subject).
            b.HasIndex(x => x.GoogleSub).IsUnique().HasDatabaseName("ux_users_google_sub");
        });

        modelBuilder.Entity<PasswordResetTokenEntity>(b =>
        {
            b.ToTable("password_reset_tokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TokenHash).HasMaxLength(100);
            // Reset lookups are by (office, token hash).
            b.HasIndex(x => new { x.OfficeId, x.TokenHash }).HasDatabaseName("ix_reset_tokens_office_hash");
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
