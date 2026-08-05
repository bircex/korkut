using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.SavedDecisions.Entities;

/// <summary>EF mapping for saved decisions ("Kararlarım").</summary>
public sealed class SavedDecisionConfiguration : IEntityTypeConfiguration<SavedDecision>
{
    public void Configure(EntityTypeBuilder<SavedDecision> builder)
    {
        builder.ToTable("saved_decisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Source).HasMaxLength(50);
        builder.Property(x => x.ExternalId).HasMaxLength(200);
        builder.Property(x => x.Title).HasMaxLength(1000);
        builder.Property(x => x.Date).HasMaxLength(50);
        builder.Property(x => x.CaseNumber).HasMaxLength(50);
        builder.Property(x => x.DecisionNumber).HasMaxLength(50);
        builder.Property(x => x.Court).HasMaxLength(200);
        builder.Property(x => x.Chamber).HasMaxLength(200);
        builder.Property(x => x.Url).HasMaxLength(2000);
        builder.Property(x => x.Snippet).HasMaxLength(2000);
        // One save per source+document per office — SaveAsync checks this first (idempotent
        // return-existing), the unique index is the defense-in-depth backstop.
        builder.HasIndex(x => new { x.OfficeId, x.Source, x.ExternalId })
            .IsUnique()
            .HasDatabaseName("ix_saved_decisions_office_source_external");
        builder.HasIndex(x => new { x.OfficeId, x.CreatedAt }).HasDatabaseName("ix_saved_decisions_office_created");
    }
}
