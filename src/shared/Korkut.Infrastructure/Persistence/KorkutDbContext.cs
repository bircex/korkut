using Korkut.Persistence;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Finance.Entities;
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
    public DbSet<Jobs.Entities.OutboxMessage> Outbox => Set<Korkut.Jobs.Entities.OutboxMessage>();

    public DbSet<Jobs.Entities.InboxEntry> Inbox => Set<Korkut.Jobs.Entities.InboxEntry>();

    public DbSet<Jobs.Entities.JobAuditRecord> JobAudit => Set<Korkut.Jobs.Entities.JobAuditRecord>();

    public DbSet<Organization.Entities.Office> Offices => Set<Korkut.Organization.Entities.Office>();

    public DbSet<Matters.Entities.Matter> Matters => Set<Korkut.Matters.Entities.Matter>();

    public DbSet<Hearings.Entities.Hearing> MatterHearings => Set<Korkut.Hearings.Entities.Hearing>();

    public DbSet<Audit.AuditEvent> AuditEvents => Set<Korkut.Audit.AuditEvent>();

    public DbSet<Organization.Entities.User> Users => Set<Korkut.Organization.Entities.User>();

    public DbSet<Organization.Entities.PasswordResetToken> PasswordResetTokens => Set<Korkut.Organization.Entities.PasswordResetToken>();

    public DbSet<Organization.Entities.Invite> Invites => Set<Korkut.Organization.Entities.Invite>();

    public DbSet<Organization.Entities.UserTotp> UserTotps => Set<Korkut.Organization.Entities.UserTotp>();

    public DbSet<Organization.Entities.RecoveryCode> RecoveryCodes => Set<Korkut.Organization.Entities.RecoveryCode>();

    public DbSet<Organization.Entities.RefreshToken> RefreshTokens => Set<Korkut.Organization.Entities.RefreshToken>();

    public DbSet<Organization.Entities.OfficeRole> OfficeCustomRoles => Set<Korkut.Organization.Entities.OfficeRole>();

    public DbSet<Clients.Entities.Client> Clients => Set<Korkut.Clients.Entities.Client>();

    public DbSet<Courts.Entities.Court> Courts => Set<Korkut.Courts.Entities.Court>();

    public DbSet<Korkut.Matters.Entities.Party> Parties => Set<Korkut.Matters.Entities.Party>();

    public DbSet<Korkut.Tasks.Entities.WorkTask> Tasks => Set<Korkut.Tasks.Entities.WorkTask>();

    public DbSet<Korkut.Deadlines.Entities.Deadline> Deadlines => Set<Korkut.Deadlines.Entities.Deadline>();

    public DbSet<Korkut.Notes.Entities.CommunicationNote> CommunicationNotes => Set<Korkut.Notes.Entities.CommunicationNote>();

    public DbSet<Korkut.Documents.Entities.Document> Documents => Set<Korkut.Documents.Entities.Document>();

    public DbSet<Korkut.Documents.Entities.DocumentExtraction> DocumentExtractions => Set<Korkut.Documents.Entities.DocumentExtraction>();

    public DbSet<Korkut.Documents.Entities.DocumentRelation> DocumentRelations => Set<Korkut.Documents.Entities.DocumentRelation>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Korkut.Correspondence.Entities.CorrespondenceItem> Correspondence => Set<Korkut.Correspondence.Entities.CorrespondenceItem>();

    public DbSet<Korkut.Settings.Entities.Persona> Personas => Set<Korkut.Settings.Entities.Persona>();

    public DbSet<Korkut.Settings.Entities.StyleProfile> StyleProfiles => Set<Korkut.Settings.Entities.StyleProfile>();

    public DbSet<Korkut.Settings.Entities.MemoryItem> MemoryItems => Set<Korkut.Settings.Entities.MemoryItem>();

    public DbSet<Korkut.Settings.Entities.MailPreference> MailPreferences => Set<Korkut.Settings.Entities.MailPreference>();

    // Chat is a general operational feature (the conversation thread is a first-class product
    // surface, not agent plumbing) so it lives in the operational DB and can relate to
    // matters/users. Generated drafts + versions remain in the Agent DB (AgentDbContext).
    public DbSet<Korkut.Chat.Entities.Conversation> Conversations => Set<Korkut.Chat.Entities.Conversation>();

    public DbSet<Korkut.Chat.Entities.ConversationMessage> ChatMessages => Set<Korkut.Chat.Entities.ConversationMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("korkut");

        modelBuilder.ApplyConfiguration(new Korkut.Jobs.Entities.OutboxMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Jobs.Entities.InboxEntryConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Jobs.Entities.JobAuditRecordConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.OfficeConfiguration());

        // Office-core mappings (matter/client/court/party/hearing) live in Configurations/
        // — kept together because their tenant-aware composite FKs form one set.
        modelBuilder.ApplyConfiguration(new Korkut.Matters.Entities.MatterConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Clients.Entities.ClientConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Courts.Entities.CourtConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Matters.Entities.PartyConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Hearings.Entities.HearingConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Audit.AuditEventConfiguration());


        modelBuilder.ApplyConfiguration(new Korkut.Tasks.Entities.TaskConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Deadlines.Entities.DeadlineConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Notes.Entities.NoteConfiguration());

        // Finance mappings live in Configurations/ (Clean Architecture layout); other
        // modules move to the same pattern module-by-module.
        modelBuilder.ApplyConfiguration(new Korkut.Finance.Entities.InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new Korkut.Finance.Entities.ExpenseConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Correspondence.Entities.CorrespondenceConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.Entities.PersonaConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.Entities.StyleProfileConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.Entities.MemoryItemConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Settings.Entities.MailPreferenceConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Chat.Entities.ConversationConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Chat.Entities.ConversationMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Documents.Entities.DocumentConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Documents.Entities.DocumentExtractionConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Documents.Entities.DocumentRelationConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.InviteConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.UserConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.PasswordResetTokenConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.UserTotpConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.RecoveryCodeConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.AdminTotpConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.AdminRecoveryCodeConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.AdminOfficeModelConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.RefreshTokenConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Organization.Entities.OfficeRoleConfiguration());

        // Cross-module FK constraints (Matter→Client, Hearing→Matter) — real DB constraints that
        // can't live in a module's own config since a module never references another's entity type.
        CrossModuleRelationships.Apply(modelBuilder);

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
