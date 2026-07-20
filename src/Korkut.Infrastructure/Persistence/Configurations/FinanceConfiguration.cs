using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Infrastructure.Persistence.Configurations;

/// <summary>EF mapping for invoices — split out of <c>KorkutDbContext.OnModelCreating</c> (Clean Architecture layout).</summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<InvoiceEntity>
{
    public void Configure(EntityTypeBuilder<InvoiceEntity> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Amount).HasColumnType("numeric(14,2)");
        builder.Property(x => x.VatRate).HasColumnType("numeric(5,4)");
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.HasIndex(x => new { x.OfficeId, x.Status }).HasDatabaseName("ix_invoices_office_status");
    }
}

/// <summary>EF mapping for expenses.</summary>
public sealed class ExpenseConfiguration : IEntityTypeConfiguration<ExpenseEntity>
{
    public void Configure(EntityTypeBuilder<ExpenseEntity> builder)
    {
        builder.ToTable("expenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Amount).HasColumnType("numeric(14,2)");
        builder.Property(x => x.Kind).HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OfficeId, x.SpentAt }).HasDatabaseName("ix_expenses_office_spent");
    }
}
