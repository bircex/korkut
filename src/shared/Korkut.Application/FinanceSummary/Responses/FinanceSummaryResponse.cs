namespace Korkut.Application.FinanceSummary.Responses;

/// <summary>The office's money roll-up: invoiced and paid are VAT-inclusive.</summary>
public sealed record FinanceSummaryResponse(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);
