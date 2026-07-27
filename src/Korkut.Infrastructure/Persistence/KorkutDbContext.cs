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

    public DbSet<Domain.Offices.Office> Offices => Set<Domain.Offices.Office>();

    public DbSet<Domain.Matters.Matter> Matters => Set<Domain.Matters.Matter>();

    public DbSet<Domain.Hearings.Hearing> MatterHearings => Set<Domain.Hearings.Hearing>();

    public DbSet<Domain.Auditing.AuditEvent> AuditEvents => Set<Domain.Auditing.AuditEvent>();

    public DbSet<Domain.Users.User> Users => Set<Domain.Users.User>();

    public DbSet<Domain.Auth.PasswordResetToken> PasswordResetTokens => Set<Domain.Auth.PasswordResetToken>();

    public DbSet<Domain.Invites.Invite> Invites => Set<Domain.Invites.Invite>();

    public DbSet<Domain.Auth.UserTotp> UserTotps => Set<Domain.Auth.UserTotp>();

    public DbSet<Domain.Auth.RecoveryCode> RecoveryCodes => Set<Domain.Auth.RecoveryCode>();

    public DbSet<Domain.Auth.RefreshToken> RefreshTokens => Set<Domain.Auth.RefreshToken>();

    public DbSet<Domain.OfficeRoles.OfficeRole> OfficeCustomRoles => Set<Domain.OfficeRoles.OfficeRole>();

    public DbSet<Korkut.Clients.Client> Clients => Set<Korkut.Clients.Client>();

    public DbSet<Domain.Courts.Court> Courts => Set<Domain.Courts.Court>();

    public DbSet<Domain.Parties.Party> Parties => Set<Domain.Parties.Party>();

    public DbSet<Domain.Tasks.WorkTask> Tasks => Set<Domain.Tasks.WorkTask>();

    public DbSet<Domain.Deadlines.Deadline> Deadlines => Set<Domain.Deadlines.Deadline>();

    public DbSet<Korkut.Notes.CommunicationNote> CommunicationNotes => Set<Korkut.Notes.CommunicationNote>();

    public DbSet<Domain.Documents.Document> Documents => Set<Domain.Documents.Document>();

    public DbSet<Domain.Documents.DocumentExtraction> DocumentExtractions => Set<Domain.Documents.DocumentExtraction>();

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

        modelBuilder.ApplyConfiguration(new Jobs.OutboxMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Jobs.InboxEntryConfiguration());

        modelBuilder.ApplyConfiguration(new Jobs.JobAuditRecordConfiguration());

        modelBuilder.ApplyConfiguration(new Offices.OfficeConfiguration());

        // Office-core mappings (matter/client/court/party/hearing) live in Configurations/
        // — kept together because their tenant-aware composite FKs form one set.
        modelBuilder.ApplyConfiguration(new Matters.MatterConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Clients.ClientConfiguration());
        modelBuilder.ApplyConfiguration(new Courts.CourtConfiguration());
        modelBuilder.ApplyConfiguration(new Parties.PartyConfiguration());
        modelBuilder.ApplyConfiguration(new Hearings.HearingConfiguration());

        modelBuilder.ApplyConfiguration(new Auditing.AuditEventConfiguration());


        modelBuilder.ApplyConfiguration(new Tasks.TaskConfiguration());
        modelBuilder.ApplyConfiguration(new Deadlines.DeadlineConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Notes.NoteConfiguration());

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

        modelBuilder.ApplyConfiguration(new Documents.DocumentExtractionConfiguration());

        modelBuilder.ApplyConfiguration(new Documents.DocumentRelationConfiguration());

        modelBuilder.ApplyConfiguration(new Invites.InviteConfiguration());

        modelBuilder.ApplyConfiguration(new Users.UserConfiguration());

        modelBuilder.ApplyConfiguration(new Auth.PasswordResetTokenConfiguration());

        modelBuilder.ApplyConfiguration(new Auth.UserTotpConfiguration());

        modelBuilder.ApplyConfiguration(new Auth.RecoveryCodeConfiguration());

        modelBuilder.ApplyConfiguration(new Auth.RefreshTokenConfiguration());

        modelBuilder.ApplyConfiguration(new OfficeRoles.OfficeRoleConfiguration());

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
