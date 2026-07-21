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
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();

    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    public DbSet<GeneratedDocumentEntity> GeneratedDocuments => Set<GeneratedDocumentEntity>();

    public DbSet<DocumentVersionEntity> DocumentVersions => Set<DocumentVersionEntity>();

    public DbSet<LlmCallEntity> LlmCalls => Set<LlmCallEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PersistenceSetup.AgentSchema);

        modelBuilder.Entity<ConversationEntity>(b =>
        {
            b.ToTable("conversations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Title).HasMaxLength(300);
            b.HasIndex(x => new { x.OfficeId, x.UpdatedAt }).HasDatabaseName("ix_conversations_office_updated");
        });

        modelBuilder.Entity<ChatMessageEntity>(b =>
        {
            b.ToTable("chat_messages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Role).HasMaxLength(20);
            b.HasIndex(x => new { x.OfficeId, x.ConversationId, x.CreatedAt }).HasDatabaseName("ix_chat_messages_conversation");
        });

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

        modelBuilder.Entity<LlmCallEntity>(b =>
        {
            b.ToTable("llm_calls");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Feature).HasMaxLength(40);
            b.Property(x => x.Provider).HasMaxLength(40);
            b.Property(x => x.ModelId).HasMaxLength(120);
            b.Property(x => x.PromptFingerprint).HasMaxLength(32);
            b.Property(x => x.Cost).HasColumnType("numeric(12,6)");
            b.HasIndex(x => new { x.OfficeId, x.CreatedAt }).HasDatabaseName("ix_llm_calls_office_created");
        });

        SnakeCaseNaming.Apply(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
