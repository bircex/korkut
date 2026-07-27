namespace Korkut.Reports.Repositories;

public sealed record MatterStats(int Total, IReadOnlyDictionary<string, int> ByStatus);

public sealed record TaskStats(int Open, int Done, int Overdue);

public sealed record AgendaStats(int Days, int HearingsUpcoming, int DeadlinesUpcoming, int DeadlinesOverdue);

public sealed record EvrakStats(int Total, IReadOnlyDictionary<string, int> ByDirection);

/// <summary>The non-finance office roll-ups (finance is composed in from the finance service).</summary>
public sealed record OfficeStats(MatterStats Matters, int Clients, TaskStats Tasks, AgendaStats Agenda, EvrakStats Evrak);
