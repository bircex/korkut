using Korkut.Organization.Entities;
using Korkut.Organization;
using Korkut.Core;

namespace Korkut.Organization.Repositories;

/// <summary>
/// Persistence port for the office's users. Listings are ordered by e-mail, the order the member
/// list is shown in.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>True if the office has an owner other than this one — the office-keeps-an-owner guard.</summary>
    Task<bool> HasAnotherOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct = default);

    /// <summary>The office's user with this (normalized) e-mail; null if there is none.</summary>
    Task<User?> FindByEmailAsync(Guid officeId, string email, CancellationToken ct = default);

    /// <summary>Tracked read by e-mail, for mutation.</summary>
    Task<User?> FindByEmailForUpdateAsync(Guid officeId, string email, CancellationToken ct = default);

    /// <summary>True if the office already has a user with this e-mail (identity is per office).</summary>
    Task<bool> ExistsWithEmailAsync(Guid officeId, string email, CancellationToken ct = default);
}
