namespace Korkut.Application.Invoices.Responses;

/// <summary>Transport shape for an invoice, including the VAT-inclusive gross.</summary>
public sealed record InvoiceResponse(
    Guid Id, Guid? ClientId, Guid? MatterId, decimal Amount, decimal VatRate, decimal Gross,
    string? Description, string Status, DateTimeOffset? IssuedAt);
