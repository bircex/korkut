using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Agent;

/// <summary>
/// The Agent DB context — the assistant's private store (conversations, messages, drafts +
/// versions) in its own schema (<c>korkut_agent</c>) with its own migration chain and RLS,
/// so it can live in a separate database/instance from the operational data. Same identity
/// + timestamp + snake_case conventions as <see cref="KorkutDbContext"/>. Tenant context is
/// set per transaction via <see cref="Tenancy.BeginTenantScopeAsync"/> (the shared
/// <c>app.current_office</c> GUC the RLS policies read).
/// </summary>
public sealed class AgentDbContext(DbContextOptions<AgentDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Agent.Conversations.Conversation> Conversations => Set<Domain.Agent.Conversations.Conversation>();

    public DbSet<Domain.Agent.Conversations.ConversationMessage> ChatMessages => Set<Domain.Agent.Conversations.ConversationMessage>();

    public DbSet<GeneratedDocumentEntity> GeneratedDocuments => Set<GeneratedDocumentEntity>();

    public DbSet<DocumentVersionEntity> DocumentVersions => Set<DocumentVersionEntity>();

    public DbSet<Domain.Agent.Ledger.LlmCall> LlmCalls => Set<Domain.Agent.Ledger.LlmCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersistenceSetup.AgentSchema);

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.ConversationConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.ConversationMessageConfiguration());

        modelBuilder.Entity<GeneratedDocumentEntity>(b =>
        {
            b.ToTable("generated_documents");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Title).HasMaxLength(300);
            b.Property(x => x.Status).HasMaxLength(20);
            b.HasIndex(x => new { x.OfficeId, x.ConversationId }).IsUnique().HasDatabaseName("ux_generated_documents_conversation");
        });

        modelBuilder.Entity<DocumentVersionEntity>(b =>
        {
            b.ToTable("document_versions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Author).HasMaxLength(320);
            b.Property(x => x.Note).HasMaxLength(500);
            b.HasIndex(x => new { x.OfficeId, x.DocumentId, x.CreatedAt }).HasDatabaseName("ix_document_versions_document");
        });

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.LlmCallConfiguration());

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
