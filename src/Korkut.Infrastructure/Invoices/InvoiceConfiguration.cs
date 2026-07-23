using Korkut.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Invoices;

/// <summary>EF mapping for invoices (table/columns unchanged). Gross is computed, never stored.</summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Amount).HasColumnType("numeric(14,2)");
        builder.Property(x => x.VatRate).HasColumnType("numeric(5,4)");
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Ignore(x => x.Gross);
        builder.Ignore(x => x.IsCancelled);
        builder.Ignore(x => x.IsPaid);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_invoices_office_status");
    }
}
