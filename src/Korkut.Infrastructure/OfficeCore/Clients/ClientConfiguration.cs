using Korkut.Domain.OfficeCore.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.OfficeCore.Clients;

/// <summary>
/// EF mapping for the <see cref="Client"/> aggregate. Table/column names are unchanged from
/// the previous persistence entity, so existing migrations and data still apply.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(400);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Phone).HasMaxLength(50);
        builder.HasAlternateKey(x => new { x.OfficeId, x.Id }); // composite FK target for matters
        builder.HasIndex(x => new { x.OfficeId, x.DeletedAt }).HasDatabaseName("ix_clients_office_deleted");
    }
}
