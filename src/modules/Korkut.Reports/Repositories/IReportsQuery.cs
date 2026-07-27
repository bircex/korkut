
namespace Korkut.Reports.Repositories;

/// <summary>
/// Read-only aggregation for the office dashboard: the matters, clients, tasks, agenda and
/// evrak roll-ups, scoped to one office (finance is composed in separately, from the
/// finance figures, so the two never disagree). There is no aggregate behind this, which
/// is why it is a query port and not a repository. <paramref name="now"/> comes from the
/// caller so the time windows stay deterministic.
/// </summary>
public interface IReportsQuery
{
    Task<OfficeStats> GetStatsAsync(Guid officeId, int days, DateTimeOffset now, CancellationToken ct = default);
}
