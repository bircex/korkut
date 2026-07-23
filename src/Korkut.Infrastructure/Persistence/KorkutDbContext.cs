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

    public DbSet<Domain.Offices.Office> Offices => Set<Domain.Offices.Office>();

    public DbSet<Domain.Matters.Matter> Matters => Set<Domain.Matters.Matter>();

    public DbSet<Domain.Hearings.Hearing> MatterHearings => Set<Domain.Hearings.Hearing>();

    public DbSet<Domain.Auditing.AuditEvent> AuditEvents => Set<Domain.Auditing.AuditEvent>();

    public DbSet<Domain.Users.User> Users => Set<Domain.Users.User>();

    public DbSet<Domain.Auth.PasswordResetToken> PasswordResetTokens => Set<Domain.Auth.PasswordResetToken>();

    public DbSet<Domain.Invites.Invite> Invites => Set<Domain.Invites.Invite>();

    public DbSet<UserTotpEntity> UserTotps => Set<UserTotpEntity>();

    public DbSet<RecoveryCodeEntity> RecoveryCodes => Set<RecoveryCodeEntity>();

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    public DbSet<OfficeCustomRoleEntity> OfficeCustomRoles => Set<OfficeCustomRoleEntity>();

    public DbSet<Domain.Clients.Client> Clients => Set<Domain.Clients.Client>();

    public DbSet<Domain.Courts.Court> Courts => Set<Domain.Courts.Court>();

    public DbSet<Domain.Parties.Party> Parties => Set<Domain.Parties.Party>();

    public DbSet<Domain.Tasks.WorkTask> Tasks => Set<Domain.Tasks.WorkTask>();

    public DbSet<Domain.Deadlines.Deadline> Deadlines => Set<Domain.Deadlines.Deadline>();

    public DbSet<Domain.Notes.CommunicationNote> CommunicationNotes => Set<Domain.Notes.CommunicationNote>();

    public DbSet<Domain.Documents.Document> Documents => Set<Domain.Documents.Document>();

    public DbSet<DocumentExtractionEntity> DocumentExtractions => Set<DocumentExtractionEntity>();

    public DbSet<Domain.Documents.DocumentRelation> DocumentRelations => Set<Domain.Documents.DocumentRelation>();

    public DbSet<Domain.Invoices.Invoice> Invoices => Set<Domain.Invoices.Invoice>();

    public DbSet<Domain.Expenses.Expense> Expenses => Set<Domain.Expenses.Expense>();

    public DbSet<Domain.Correspondence.CorrespondenceItem> Correspondence => Set<Domain.Correspondence.CorrespondenceItem>();

    public DbSet<Domain.Calendar.CalendarEvent> CalendarEvents => Set<Domain.Calendar.CalendarEvent>();

    public DbSet<Domain.Settings.Persona> Personas => Set<Domain.Settings.Persona>();

    public DbSet<Domain.Settings.StyleProfile> StyleProfiles => Set<Domain.Settings.StyleProfile>();

    public DbSet<Domain.Settings.MemoryItem> MemoryItems => Set<Domain.Settings.MemoryItem>();

    public DbSet<Domain.Channels.Channel> Channels => Set<Domain.Channels.Channel>();


    // Conversations, chat_messages, generated_documents, document_versions moved to the
    // Agent DB (AgentDbContext, schema korkut_agent).
    public DbSet<Domain.Outbox.OutboundMessage> OutboundMessages => Set<Domain.Outbox.OutboundMessage>();

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

        modelBuilder.ApplyConfiguration(new Offices.OfficeConfiguration());

        // Office-core mappings (matter/client/court/party/hearing) live in Configurations/
        // — kept together because their tenant-aware composite FKs form one set.
        modelBuilder.ApplyConfiguration(new Matters.MatterConfiguration());
        modelBuilder.ApplyConfiguration(new Clients.ClientConfiguration());
        modelBuilder.ApplyConfiguration(new Courts.CourtConfiguration());
        modelBuilder.ApplyConfiguration(new Parties.PartyConfiguration());
        modelBuilder.ApplyConfiguration(new Hearings.HearingConfiguration());

        modelBuilder.ApplyConfiguration(new Auditing.AuditEventConfiguration());


        modelBuilder.ApplyConfiguration(new Tasks.TaskConfiguration());
        modelBuilder.ApplyConfiguration(new Deadlines.DeadlineConfiguration());
        modelBuilder.ApplyConfiguration(new Notes.NoteConfiguration());

        // Finance mappings live in Configurations/ (Clean Architecture layout); other
        // modules move to the same pattern module-by-module.
        modelBuilder.ApplyConfiguration(new Invoices.InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new Expenses.ExpenseConfiguration());

        modelBuilder.ApplyConfiguration(new Correspondence.CorrespondenceConfiguration());

        modelBuilder.ApplyConfiguration(new Calendar.CalendarEventConfiguration());

        modelBuilder.ApplyConfiguration(new Settings.PersonaConfiguration());

        modelBuilder.ApplyConfiguration(new Settings.StyleProfileConfiguration());

        modelBuilder.ApplyConfiguration(new Settings.MemoryItemConfiguration());

        modelBuilder.ApplyConfiguration(new Channels.ChannelConfiguration());

        modelBuilder.ApplyConfiguration(new Outbox.OutboundMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Documents.DocumentConfiguration());

        modelBuilder.Entity<DocumentExtractionEntity>(b =>
        {
            b.ToTable("document_extractions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ParserVersion).HasMaxLength(50);
            b.Property(x => x.SourceChecksumSha256).HasMaxLength(64);
            b.Property(x => x.SourceObjectKey).HasMaxLength(100);
            // Tenant-aware composite FK to the source document.
            b.HasOne<Domain.Documents.Document>()
                .WithMany()
                .HasForeignKey(x => new { x.OfficeId, x.DocumentId })
                .HasPrincipalKey(d => new { d.OfficeId, d.Id })
                .OnDelete(DeleteBehavior.Cascade);
            // Retry idempotency + immutable-per-parser: one row per source span+parser.
            b.HasIndex(x => new { x.OfficeId, x.DocumentId, x.DocumentVersion, x.ParserVersion, x.Page, x.SpanIndex })
                .IsUnique()
                .HasDatabaseName("ux_doc_extractions_provenance");
        });

        modelBuilder.ApplyConfiguration(new Documents.DocumentRelationConfiguration());

        modelBuilder.ApplyConfiguration(new Invites.InviteConfiguration());

        modelBuilder.ApplyConfiguration(new Users.UserConfiguration());

        modelBuilder.ApplyConfiguration(new Auth.PasswordResetTokenConfiguration());

        modelBuilder.Entity<UserTotpEntity>(b =>
        {
            b.ToTable("user_totp");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EncryptedSecret).HasMaxLength(500);
            // One TOTP enrollment per user.
            b.HasIndex(x => new { x.OfficeId, x.UserId }).IsUnique().HasDatabaseName("ux_user_totp_office_user");
        });

        modelBuilder.Entity<RecoveryCodeEntity>(b =>
        {
            b.ToTable("recovery_codes");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CodeHash).HasMaxLength(100);
            b.HasIndex(x => new { x.OfficeId, x.UserId }).HasDatabaseName("ix_recovery_codes_office_user");
        });

        modelBuilder.Entity<RefreshTokenEntity>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TokenHash).HasMaxLength(100);
            b.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_hash");
            b.HasIndex(x => x.FamilyId).HasDatabaseName("ix_refresh_tokens_family");
        });

        modelBuilder.Entity<OfficeCustomRoleEntity>(b =>
        {
            b.ToTable("office_custom_roles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Key).HasMaxLength(50);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.Permissions).HasColumnType("text[]");
            b.HasIndex(x => new { x.OfficeId, x.Key }).IsUnique().HasDatabaseName("ux_custom_roles_office_key");
        });

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
