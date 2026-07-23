namespace Korkut.Application.Reports.Responses;

/// <summary>The whole office dashboard in one payload.</summary>
public sealed record ReportsOverviewResponse(
    MatterStatsResponse Matters, ClientStatsResponse Clients, FinanceStatsResponse Finance,
    TaskStatsResponse Tasks, AgendaStatsResponse Agenda, EvrakStatsResponse Evrak);
