using Korkut.Domain.Reports;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>
/// Read-only aggregation port for the office dashboard: matters/clients/tasks/agenda/evrak
/// roll-ups scoped to the office (finance is composed in separately). <paramref name="now"/>
/// is supplied by the caller so the time windows are deterministic/testable.
/// </summary>
public interface IReportsRepository
{
    Task<OfficeStats> GetStatsAsync(Guid officeId, int days, DateTimeOffset now, CancellationToken ct = default);
}
