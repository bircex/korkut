using Korkut.Application.Jobs;
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
    public DbSet<Domain.Jobs.OutboxMessage> Outbox => Set<Domain.Jobs.OutboxMessage>();

    public DbSet<Domain.Jobs.InboxEntry> Inbox => Set<Domain.Jobs.InboxEntry>();

    public DbSet<Domain.Jobs.JobAuditRecord> JobAudit => Set<Domain.Jobs.JobAuditRecord>();

    public DbSet<Korkut.Organization.Office> Offices => Set<Korkut.Organization.Office>();

    public DbSet<Korkut.Matters.Matter> Matters => Set<Korkut.Matters.Matter>();

    public DbSet<Korkut.Hearings.Hearing> MatterHearings => Set<Korkut.Hearings.Hearing>();

    public DbSet<Korkut.Infrastructure.Auditing.AuditEvent> AuditEvents => Set<Korkut.Infrastructure.Auditing.AuditEvent>();

    public DbSet<Korkut.Organization.User> Users => Set<Korkut.Organization.User>();

    public DbSet<Korkut.Organization.PasswordResetToken> PasswordResetTokens => Set<Korkut.Organization.PasswordResetToken>();

    public DbSet<Korkut.Organization.Invite> Invites => Set<Korkut.Organization.Invite>();

    public DbSet<Korkut.Organization.UserTotp> UserTotps => Set<Korkut.Organization.UserTotp>();

    public DbSet<Korkut.Organization.RecoveryCode> RecoveryCodes => Set<Korkut.Organization.RecoveryCode>();

    public DbSet<Korkut.Organization.RefreshToken> RefreshTokens => Set<Korkut.Organization.RefreshToken>();

    public DbSet<Korkut.Organization.OfficeRole> OfficeCustomRoles => Set<Korkut.Organization.OfficeRole>();

    public DbSet<Korkut.Clients.Client> Clients => Set<Korkut.Clients.Client>();

    public DbSet<Korkut.Courts.Court> Courts => Set<Korkut.Courts.Court>();

    public DbSet<Korkut.Matters.Party> Parties => Set<Korkut.Matters.Party>();

    public DbSet<Korkut.Tasks.WorkTask> Tasks => Set<Korkut.Tasks.WorkTask>();

    public DbSet<Korkut.Deadlines.Deadline> Deadlines => Set<Korkut.Deadlines.Deadline>();

    public DbSet<Korkut.Notes.CommunicationNote> CommunicationNotes => Set<Korkut.Notes.CommunicationNote>();

    public DbSet<Korkut.Documents.Document> Documents => Set<Korkut.Documents.Document>();

    public DbSet<Korkut.Documents.DocumentExtraction> DocumentExtractions => Set<Korkut.Documents.DocumentExtraction>();

    public DbSet<Korkut.Documents.DocumentRelation> DocumentRelations => Set<Korkut.Documents.DocumentRelation>();

    public DbSet<Korkut.Invoices.Invoice> Invoices => Set<Korkut.Invoices.Invoice>();

    public DbSet<Korkut.Expenses.Expense> Expenses => Set<Korkut.Expenses.Expense>();

    public DbSet<Korkut.Correspondence.CorrespondenceItem> Correspondence => Set<Korkut.Correspondence.CorrespondenceItem>();

    public DbSet<Korkut.Calendar.CalendarEvent> CalendarEvents => Set<Korkut.Calendar.CalendarEvent>();

    public DbSet<Korkut.Settings.Persona> Personas => Set<Korkut.Settings.Persona>();

    public DbSet<Korkut.Settings.StyleProfile> StyleProfiles => Set<Korkut.Settings.StyleProfile>();

    public DbSet<Korkut.Settings.MemoryItem> MemoryItems => Set<Korkut.Settings.MemoryItem>();

    public DbSet<Korkut.Channels.Channel> Channels => Set<Korkut.Channels.Channel>();


    // Conversations, chat_messages, generated_documents, document_versions moved to the
    // Agent DB (AgentDbContext, schema korkut_agent).
    public DbSet<Korkut.Outbox.OutboundMessage> OutboundMessages => Set<Korkut.Outbox.OutboundMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("korkut");

        modelBuilder.ApplyConfiguration(new Jobs.OutboxMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Jobs.InboxEntryConfiguration());

        modelBuilder.ApplyConfiguration(new Jobs.JobAuditRecordConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.OfficeConfiguration());

        // Office-core mappings (matter/client/court/party/hearing) live in Configurations/
        // — kept together because their tenant-aware composite FKs form one set.
        modelBuilder.ApplyConfiguration(new Korkut.Matters.MatterConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Clients.ClientConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Courts.CourtConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Matters.PartyConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Hearings.HearingConfiguration());

        modelBuilder.ApplyConfiguration(new Auditing.AuditEventConfiguration());


        modelBuilder.ApplyConfiguration(new Korkut.Tasks.TaskConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Deadlines.DeadlineConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Notes.NoteConfiguration());

        // Finance mappings live in Configurations/ (Clean Architecture layout); other
        // modules move to the same pattern module-by-module.
        modelBuilder.ApplyConfiguration(new Korkut.Invoices.InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Expenses.ExpenseConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Correspondence.CorrespondenceConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Calendar.CalendarEventConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.PersonaConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.StyleProfileConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.MemoryItemConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Channels.ChannelConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Outbox.OutboundMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Documents.DocumentConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Documents.DocumentExtractionConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Documents.DocumentRelationConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.InviteConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.UserConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.PasswordResetTokenConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.UserTotpConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.RecoveryCodeConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.RefreshTokenConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.OfficeRoleConfiguration());

        // Cross-module FK constraints (Matter→Client, Hearing→Matter) — real DB constraints that
        // can't live in a module's own config since a module never references another's entity type.
        CrossModuleRelationships.Apply(modelBuilder);

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
