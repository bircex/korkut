using Korkut.Application.Common;
using Korkut.Domain.Users;

namespace Korkut.Application.Users;

/// <summary>
/// Persistence port for the office's users. Listings are ordered by e-mail, the order the
/// member list is shown in.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>True if the office has an owner other than this one — the office-keeps-an-owner guard.</summary>
    Task<bool> HasAnotherOwnerAsync(Guid officeId, Guid excludingId, CancellationToken ct = default);
}
