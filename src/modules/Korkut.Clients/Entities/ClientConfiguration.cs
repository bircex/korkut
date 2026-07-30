using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Clients.Entities;

/// <summary>
/// EF mapping for the <see cref="Client"/> aggregate.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(400);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.Property(x => x.NationalId).HasColumnName("national_id").HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
        builder.HasAlternateKey(x => new { x.OfficeId, x.Id }); // composite FK target for matters
        builder.HasIndex(x => new { x.OfficeId, x.DeletedAt }).HasDatabaseName("ix_clients_office_deleted");
    }
}
