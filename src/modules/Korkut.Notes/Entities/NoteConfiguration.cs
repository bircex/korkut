using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Notes.Entities;

/// <summary>EF mapping for communication notes (table/columns unchanged).</summary>
public sealed class NoteConfiguration : IEntityTypeConfiguration<CommunicationNote>
{
    public void Configure(EntityTypeBuilder<CommunicationNote> builder)
    {
        builder.ToTable("communication_notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Channel).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.MatterId }).HasDatabaseName("ix_comm_notes_office_matter");
    }
}
