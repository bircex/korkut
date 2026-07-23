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

    public DbSet<InviteEntity> Invites => Set<InviteEntity>();

    public DbSet<UserTotpEntity> UserTotps => Set<UserTotpEntity>();

    public DbSet<RecoveryCodeEntity> RecoveryCodes => Set<RecoveryCodeEntity>();

    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    public DbSet<OfficeCustomRoleEntity> OfficeCustomRoles => Set<OfficeCustomRoleEntity>();

    public DbSet<Domain.OfficeCore.Clients.Client> Clients => Set<Domain.OfficeCore.Clients.Client>();

    public DbSet<CourtEntity> Courts => Set<CourtEntity>();

    public DbSet<PartyEntity> Parties => Set<PartyEntity>();

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

    public DbSet<DeadlineEntity> Deadlines => Set<DeadlineEntity>();

    public DbSet<CommunicationNoteEntity> CommunicationNotes => Set<CommunicationNoteEntity>();

    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

    public DbSet<DocumentExtractionEntity> DocumentExtractions => Set<DocumentExtractionEntity>();

    public DbSet<DocumentRelationEntity> DocumentRelations => Set<DocumentRelationEntity>();

    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();

    public DbSet<ExpenseEntity> Expenses => Set<ExpenseEntity>();

    public DbSet<CorrespondenceEntity> Correspondence => Set<CorrespondenceEntity>();

    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();

    public DbSet<PersonaEntity> Personas => Set<PersonaEntity>();

    public DbSet<StyleProfileEntity> StyleProfiles => Set<StyleProfileEntity>();

    public DbSet<MemoryItemEntity> MemoryItems => Set<MemoryItemEntity>();

    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();

    public DbSet<TemplateOverrideEntity> TemplateOverrides => Set<TemplateOverrideEntity>();

    // Conversations, chat_messages, generated_documents, document_versions moved to the
    // Agent DB (AgentDbContext, schema korkut_agent).
    public DbSet<OutboundMessageEntity> OutboundMessages => Set<OutboundMessageEntity>();

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

        // Office-core mappings (matter/client/court/party/hearing) live in Configurations/
        // — kept together because their tenant-aware composite FKs form one set.
        modelBuilder.ApplyConfiguration(new Configurations.MatterConfiguration());
        modelBuilder.ApplyConfiguration(new OfficeCore.Clients.ClientConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.CourtConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.PartyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.MatterHearingConfiguration());

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


        modelBuilder.ApplyConfiguration(new Configurations.TaskConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DeadlineConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.CommunicationNoteConfiguration());

        // Finance mappings live in Configurations/ (Clean Architecture layout); other
        // modules move to the same pattern module-by-module.
        modelBuilder.ApplyConfiguration(new Configurations.InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ExpenseConfiguration());

        modelBuilder.ApplyConfiguration(new Configurations.CorrespondenceConfiguration());

        modelBuilder.Entity<CalendarEventEntity>(b =>
        {
            b.ToTable("calendar_events");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Category).HasMaxLength(20);
            b.Property(x => x.Type).HasMaxLength(100);
            b.Property(x => x.Title).HasMaxLength(500);
            b.Property(x => x.Location).HasMaxLength(400);
            b.Property(x => x.Status).HasMaxLength(20);
            b.Property(x => x.Result).HasMaxLength(1000);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Rrule).HasMaxLength(400);
            b.Property(x => x.ReminderStatus).HasMaxLength(20);
            b.HasIndex(x => new { x.OfficeId, x.StartAt }).HasDatabaseName("ix_calendar_events_office_start");
        });

        modelBuilder.Entity<PersonaEntity>(b =>
        {
            b.ToTable("office_persona");
            b.HasKey(x => x.OfficeId); // exactly one persona row per office
            b.Property(x => x.OfficeId).ValueGeneratedNever();
            b.Property(x => x.Tone).HasMaxLength(2000);
            b.Property(x => x.Expertise).HasMaxLength(2000);
            b.Property(x => x.ExtraRules).HasMaxLength(4000);
        });

        modelBuilder.Entity<StyleProfileEntity>(b =>
        {
            b.ToTable("style_profiles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Scope).HasMaxLength(10);
            b.Property(x => x.Letterhead).HasMaxLength(2000);
            b.Property(x => x.Signature).HasMaxLength(2000);
            b.Property(x => x.Tone).HasMaxLength(2000);
            b.Property(x => x.Boilerplate).HasMaxLength(4000);
            // One office profile, and at most one per user, per office.
            b.HasIndex(x => new { x.OfficeId, x.Scope, x.UserId })
                .IsUnique().HasDatabaseName("ux_style_profiles_office_scope_user");
        });

        modelBuilder.Entity<MemoryItemEntity>(b =>
        {
            b.ToTable("memory_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Kind).HasMaxLength(50);
            b.Property(x => x.Text).HasMaxLength(4000);
            b.Property(x => x.Status).HasMaxLength(20);
            b.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_memory_items_office_status");
        });

        modelBuilder.Entity<ChannelEntity>(b =>
        {
            b.ToTable("channels");
            b.HasKey(x => new { x.OfficeId, x.Kind }); // one config per kind per office
            b.Property(x => x.Kind).HasMaxLength(20);
            b.Property(x => x.FromAddress).HasMaxLength(320);
            b.Property(x => x.FromName).HasMaxLength(200);
            b.Property(x => x.BaseUrl).HasMaxLength(400);
            b.Property(x => x.LinkedId).HasMaxLength(200);
            b.Property(x => x.LinkCode).HasMaxLength(40);
        });

        modelBuilder.Entity<TemplateOverrideEntity>(b =>
        {
            b.ToTable("template_overrides");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Scope).HasMaxLength(10);
            b.Property(x => x.Kind).HasMaxLength(20);
            b.Property(x => x.TypeId).HasMaxLength(100);
            // One override per (office, scope, user, kind, type).
            b.HasIndex(x => new { x.OfficeId, x.Scope, x.UserId, x.Kind, x.TypeId })
                .IsUnique().HasDatabaseName("ux_template_overrides_key");
        });

        modelBuilder.Entity<OutboundMessageEntity>(b =>
        {
            b.ToTable("outbound_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Kind).HasMaxLength(20);
            b.Property(x => x.ToAddr).HasMaxLength(400);
            b.Property(x => x.Subject).HasMaxLength(500);
            b.Property(x => x.Status).HasMaxLength(20);
            b.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_outbound_messages_office_status");
        });

        modelBuilder.Entity<DocumentEntity>(b =>
        {
            b.ToTable("documents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Bucket).HasMaxLength(63);
            b.Property(x => x.ObjectKey).HasMaxLength(100);
            b.Property(x => x.OriginalFilename).HasMaxLength(500);
            b.Property(x => x.ContentType).HasMaxLength(200);
            b.Property(x => x.ChecksumSha256).HasMaxLength(64);
            b.Property(x => x.Status).HasMaxLength(20);
            b.HasIndex(x => x.ObjectKey).IsUnique().HasDatabaseName("ux_documents_object_key");
            b.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_documents_office_matter");
            b.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_documents_office_status");
        });

        modelBuilder.Entity<DocumentExtractionEntity>(b =>
        {
            b.ToTable("document_extractions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ParserVersion).HasMaxLength(50);
            b.Property(x => x.SourceChecksumSha256).HasMaxLength(64);
            b.Property(x => x.SourceObjectKey).HasMaxLength(100);
            // Tenant-aware composite FK to the source document.
            b.HasOne<DocumentEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.OfficeId, x.DocumentId })
                .HasPrincipalKey(d => new { d.OfficeId, d.Id })
                .OnDelete(DeleteBehavior.Cascade);
            // Retry idempotency + immutable-per-parser: one row per source span+parser.
            b.HasIndex(x => new { x.OfficeId, x.DocumentId, x.DocumentVersion, x.ParserVersion, x.Page, x.SpanIndex })
                .IsUnique()
                .HasDatabaseName("ux_doc_extractions_provenance");
        });

        modelBuilder.Entity<DocumentRelationEntity>(b =>
        {
            b.ToTable("document_relations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TargetType).HasMaxLength(20);
            // Tenant-aware composite FK to the source document.
            b.HasOne<DocumentEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.OfficeId, x.DocumentId })
                .HasPrincipalKey(d => new { d.OfficeId, d.Id })
                .OnDelete(DeleteBehavior.Cascade);
            // One link per (document, target) — makes multi-linking idempotent.
            b.HasIndex(x => new { x.OfficeId, x.DocumentId, x.TargetType, x.TargetId })
                .IsUnique()
                .HasDatabaseName("ux_doc_relations_link");
            b.HasIndex(x => new { x.OfficeId, x.TargetType, x.TargetId })
                .HasDatabaseName("ix_doc_relations_target");
        });

        modelBuilder.Entity<InviteEntity>(b =>
        {
            b.ToTable("invites");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.Role).HasMaxLength(20);
            b.Property(x => x.TokenHash).HasMaxLength(64);
            b.HasIndex(x => x.TokenHash).HasDatabaseName("ix_invites_token_hash");
            b.HasIndex(x => new { x.OfficeId, x.Email }).HasDatabaseName("ix_invites_office_email");
        });

        modelBuilder.Entity<UserEntity>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Email).HasMaxLength(320);
            b.Property(x => x.FullName).HasMaxLength(200);
            b.Property(x => x.BarRegistryNo).HasMaxLength(50);
            b.Property(x => x.PasswordHash).HasMaxLength(400);
            b.Property(x => x.Role).HasMaxLength(50);
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
