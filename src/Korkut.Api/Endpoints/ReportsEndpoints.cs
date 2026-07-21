using System.Security.Claims;
using Korkut.Api.Tenancy;
using Korkut.Application.Features.Reports;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Api.Endpoints;

public sealed record MatterStatsResponse(int Total, IReadOnlyDictionary<string, int> ByStatus);

public sealed record ClientStatsResponse(int Total);

public sealed record FinanceStatsResponse(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);

public sealed record TaskStatsResponse(int Open, int Done, int Overdue);

public sealed record AgendaStatsResponse(int Days, int HearingsUpcoming, int DeadlinesUpcoming, int DeadlinesOverdue);

public sealed record EvrakStatsResponse(int Total, IReadOnlyDictionary<string, int> ByDirection);

public sealed record ReportsOverviewResponse(
    MatterStatsResponse Matters, ClientStatsResponse Clients, FinanceStatsResponse Finance,
    TaskStatsResponse Tasks, AgendaStatsResponse Agenda, EvrakStatsResponse Evrak);

/// <summary>
/// Read-only office dashboard (KDM reports). Authenticated, tenant-scoped (RLS). The
/// overview rolls up matters/clients/finance/tasks/agenda/evrak; because it surfaces
/// money it requires billing.read (both built-in roles hold it).
/// </summary>
public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/overview", async (int? days, ClaimsPrincipal user, KorkutDbContext db, IReportsService svc, CancellationToken ct) =>
            {
                var window = Math.Clamp(days ?? 14, 1, 365);
                var o = await user.InTenantScopeAsync(db, office => svc.OverviewAsync(office, window, ct), ct);
                return Results.Ok(new ReportsOverviewResponse(
                    new MatterStatsResponse(o.Matters.Total, o.Matters.ByStatus),
                    new ClientStatsResponse(o.Clients),
                    new FinanceStatsResponse(o.Finance.Invoiced, o.Finance.Paid, o.Finance.Outstanding, o.Finance.Expenses),
                    new TaskStatsResponse(o.Tasks.Open, o.Tasks.Done, o.Tasks.Overdue),
                    new AgendaStatsResponse(o.Agenda.Days, o.Agenda.HearingsUpcoming, o.Agenda.DeadlinesUpcoming, o.Agenda.DeadlinesOverdue),
                    new EvrakStatsResponse(o.Evrak.Total, o.Evrak.ByDirection)));
            })
            .RequireAuthorization(Permissions.BillingRead);

        return app;
    }
}
