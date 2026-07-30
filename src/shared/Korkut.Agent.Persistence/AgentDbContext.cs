using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Persistence;

/// <summary>
/// The Agent DB context — the assistant's private store (generated drafts + versions, and the
/// LLM usage/cost ledger) in its own schema (<c>korkut_agent</c>) with its own migration chain
/// and RLS. Conversations/messages are NOT here: chat is a general operational feature that
/// lives in <see cref="KorkutDbContext"/>. Kept separate so the AI plane can live in a
/// separate database/instance from the operational data. Same identity
/// + timestamp + snake_case conventions as <see cref="KorkutDbContext"/>. Tenant context is
/// set per transaction via <see cref="Tenancy.BeginTenantScopeAsync"/> (the shared
/// <c>app.current_office</c> GUC the RLS policies read).
/// </summary>
public sealed class AgentDbContext(DbContextOptions<AgentDbContext> options) : DbContext(options)
{
    public DbSet<Korkut.Agent.Drafting.Entities.GeneratedDocument> GeneratedDocuments => Set<Korkut.Agent.Drafting.Entities.GeneratedDocument>();

    public DbSet<Korkut.Agent.Drafting.Entities.DocumentVersion> DocumentVersions => Set<Korkut.Agent.Drafting.Entities.DocumentVersion>();

    public DbSet<Korkut.Agent.Ledger.Entities.LlmCall> LlmCalls => Set<Korkut.Agent.Ledger.Entities.LlmCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(AgentPersistenceSetup.AgentSchema);

        modelBuilder.ApplyConfiguration(new Korkut.Agent.Drafting.Entities.GeneratedDocumentConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Agent.Drafting.Entities.DocumentVersionConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Agent.Ledger.Entities.LlmCallConfiguration());

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
