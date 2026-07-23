using Korkut.Domain.Common;
namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned invoice raised against a client and/or matter. Money is stored as
/// exact <see cref="decimal"/> (never float). The gross (VAT-inclusive) total is
/// derived from <see cref="Amount"/> and <see cref="VatRate"/> at read time, never
/// stored, so the two can't drift.
/// </summary>
public sealed class InvoiceEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>Optional tenant-aware link to the billed client.</summary>
    public Guid? ClientId { get; set; }

    /// <summary>Optional tenant-aware link to the matter this invoice belongs to.</summary>
    public Guid? MatterId { get; set; }

    /// <summary>Net amount before VAT.</summary>
    public decimal Amount { get; set; }

    /// <summary>VAT rate as a fraction (0.20 = 20%).</summary>
    public decimal VatRate { get; set; }

    public string? Description { get; set; }

    /// <summary>"draft" | "sent" | "paid" | "cancelled".</summary>
    public string Status { get; set; } = "draft";

    public DateTimeOffset? IssuedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A tenant-owned expense/disbursement, optionally attributed to a matter/client.</summary>
public sealed class ExpenseEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid? MatterId { get; set; }

    public Guid? ClientId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Free-form category, e.g. "harç" | "yol" | "bilirkişi".</summary>
    public string? Kind { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset? SpentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
