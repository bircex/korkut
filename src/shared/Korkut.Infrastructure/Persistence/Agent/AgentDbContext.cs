using Korkut.Persistence;
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

    public DbSet<Domain.Agent.Documents.GeneratedDocument> GeneratedDocuments => Set<Domain.Agent.Documents.GeneratedDocument>();

    public DbSet<Domain.Agent.Documents.DocumentVersion> DocumentVersions => Set<Domain.Agent.Documents.DocumentVersion>();

    public DbSet<Domain.Agent.Ledger.LlmCall> LlmCalls => Set<Domain.Agent.Ledger.LlmCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersistenceSetup.AgentSchema);

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.ConversationConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.ConversationMessageConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.GeneratedDocumentConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.DocumentVersionConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Infrastructure.Agent.Persistence.LlmCallConfiguration());

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
