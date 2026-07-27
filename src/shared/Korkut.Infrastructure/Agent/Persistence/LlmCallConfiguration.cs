using Korkut.Domain.Agent.Ledger;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Agent.Persistence;

/// <summary>EF mapping for the llm_calls ledger in the Agent DB (table/columns/index unchanged).</summary>
public sealed class LlmCallConfiguration : IEntityTypeConfiguration<LlmCall>
{
    public void Configure(EntityTypeBuilder<LlmCall> builder)
    {
        builder.ToTable("llm_calls");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Feature).HasMaxLength(40);
        builder.Property(x => x.Provider).HasMaxLength(40);
        builder.Property(x => x.ModelId).HasMaxLength(120);
        builder.Property(x => x.PromptFingerprint).HasMaxLength(32);
        builder.Property(x => x.Cost).HasColumnType("numeric(12,6)");
        builder.HasIndex(x => new { x.OfficeId, x.CreatedAt }).HasDatabaseName("ix_llm_calls_office_created");
    }
}
