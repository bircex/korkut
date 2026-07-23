using Korkut.Application.Offices.Responses;

namespace Korkut.Application.Offices;

/// <summary>
/// Platform-admin office management: list every office, suspend or restore one, and the
/// headline counts. Deliberately cross-tenant — the caller is a platform admin, not a
/// member of any office, so none of this runs in a tenant scope.
/// </summary>
public interface IOfficeAdminService
{
    Task<IReadOnlyList<OfficeResponse>> ListAsync(CancellationToken ct = default);

    Task SetDisabledAsync(Guid officeId, bool disabled, CancellationToken ct = default);

    Task<OfficeStatsResponse> StatsAsync(CancellationToken ct = default);
}
