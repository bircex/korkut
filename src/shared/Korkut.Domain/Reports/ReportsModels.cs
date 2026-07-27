namespace Korkut.Domain.Reports;

/// <summary>The office's money roll-up (VAT-inclusive), composed into the dashboard.</summary>
public sealed record FinanceTotals(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);

public sealed record MatterStats(int Total, IReadOnlyDictionary<string, int> ByStatus);

public sealed record TaskStats(int Open, int Done, int Overdue);

public sealed record AgendaStats(int Days, int HearingsUpcoming, int DeadlinesUpcoming, int DeadlinesOverdue);

public sealed record EvrakStats(int Total, IReadOnlyDictionary<string, int> ByDirection);

/// <summary>The non-finance office roll-ups (finance is composed in from the finance service).</summary>
public sealed record OfficeStats(MatterStats Matters, int Clients, TaskStats Tasks, AgendaStats Agenda, EvrakStats Evrak);

/// <summary>The full read-only office dashboard.</summary>
public sealed record ReportsOverview(
    MatterStats Matters, int Clients, FinanceTotals Finance, TaskStats Tasks, AgendaStats Agenda, EvrakStats Evrak);
