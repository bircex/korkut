using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Invoices;

/// <summary>Body for issuing an invoice.</summary>
public sealed record CreateInvoiceRequest
{
    [Range(0, 100_000_000)]
    public decimal Amount { get; init; }

    /// <summary>VAT rate as a fraction (0.20 = 20%). Defaults to Turkey's standard 20%.</summary>
    [Range(0, 1)]
    public decimal VatRate { get; init; } = 0.20m;

    public Guid? ClientId { get; init; }

    public Guid? MatterId { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [RegularExpression("draft|sent|paid|cancelled")]
    public string Status { get; init; } = "draft";

    public DateTimeOffset? IssuedAt { get; init; }
}

/// <summary>Partial update of an invoice; omitted fields keep their current value.</summary>
public sealed record UpdateInvoiceRequest
{
    [RegularExpression("draft|sent|paid|cancelled")]
    public string? Status { get; init; }

    [Range(0, 100_000_000)]
    public decimal? Amount { get; init; }
}
