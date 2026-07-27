namespace Korkut.Contract.Reports;

/// <summary>What the next few days hold, and what has already slipped.</summary>
public sealed record AgendaStatsResponse(int Days, int HearingsUpcoming, int DeadlinesUpcoming, int DeadlinesOverdue);
