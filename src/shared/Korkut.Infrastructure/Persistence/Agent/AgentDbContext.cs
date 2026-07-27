using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Agent;

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
    public DbSet<Korkut.Agent.Drafting.GeneratedDocument> GeneratedDocuments => Set<Korkut.Agent.Drafting.GeneratedDocument>();

    public DbSet<Korkut.Agent.Drafting.DocumentVersion> DocumentVersions => Set<Korkut.Agent.Drafting.DocumentVersion>();

    public DbSet<Korkut.Agent.Ledger.Entities.LlmCall> LlmCalls => Set<Korkut.Agent.Ledger.Entities.LlmCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersistenceSetup.AgentSchema);

        modelBuilder.ApplyConfiguration(new Korkut.Agent.Drafting.GeneratedDocumentConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Agent.Drafting.DocumentVersionConfiguration());

        modelBuilder.ApplyConfiguration(new Korkut.Agent.Ledger.Entities.LlmCallConfiguration());

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
