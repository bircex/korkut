using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Invoices.Requests;

/// <summary>Partial update of an invoice; omitted fields keep their current value.</summary>
public sealed record UpdateInvoiceRequest
{
    [RegularExpression("draft|sent|paid|cancelled")]
    public string? Status { get; init; }

    [Range(0, 100_000_000)]
    public decimal? Amount { get; init; }
}
