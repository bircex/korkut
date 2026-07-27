using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Settings.Entities;

/// <summary>EF mapping for the office persona (table/columns unchanged; keyed by office).</summary>
public sealed class PersonaConfiguration : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> builder)
    {
        builder.ToTable("office_persona");
        builder.HasKey(x => x.OfficeId); // exactly one persona row per office
        builder.Property(x => x.OfficeId).ValueGeneratedNever();
        builder.Property(x => x.Tone).HasMaxLength(2000);
        builder.Property(x => x.Expertise).HasMaxLength(2000);
        builder.Property(x => x.ExtraRules).HasMaxLength(4000);
    }
}
