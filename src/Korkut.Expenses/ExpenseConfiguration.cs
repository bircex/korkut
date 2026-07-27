using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Expenses;

/// <summary>EF mapping for expenses (table/columns unchanged).</summary>
public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
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
