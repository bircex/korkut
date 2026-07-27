namespace Korkut.Application.Reports.Responses;

/// <summary>The office's money roll-up (VAT-inclusive), as the dashboard shows it.</summary>
public sealed record FinanceStatsResponse(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);
