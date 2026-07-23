namespace Korkut.Domain.Common;

/// <summary>
/// A tenant-owned entity: every row carries its owning office. This is the platform's most
/// important invariant, enforced three ways: Postgres RLS, the office-first repository API,
/// and the base repository's mandatory office scope.
/// </summary>
public interface ITenantOwned
{
    Guid OfficeId { get; }
}
