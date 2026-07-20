namespace Korkut.Domain.Finance;

/// <summary>
/// A tenant-owned invoice (domain model, persistence-agnostic). Money is exact
/// <see cref="decimal"/>; the VAT-inclusive gross is DERIVED here, never stored, so the
/// net amount and gross can never drift.
/// </summary>
public sealed record Invoice(
    Guid Id,
    Guid OfficeId,
    Guid? ClientId,
    Guid? MatterId,
    decimal Amount,
    decimal VatRate,
    string? Description,
    string Status,
    DateTimeOffset? IssuedAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>VAT-inclusive total, rounded to 2 dp (banker's rounding).</summary>
    public decimal Gross => Math.Round(Amount * (1m + VatRate), 2, MidpointRounding.ToEven);
}

/// <summary>A tenant-owned expense/disbursement (domain model).</summary>
public sealed record Expense(
    Guid Id,
    Guid OfficeId,
    Guid? MatterId,
    Guid? ClientId,
    decimal Amount,
    string? Kind,
    string? Description,
    DateTimeOffset? SpentAt,
    DateTimeOffset CreatedAt);

/// <summary>Aggregate money view of an office: invoiced/paid/outstanding (gross) + total expenses.</summary>
public readonly record struct FinanceTotals(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);
