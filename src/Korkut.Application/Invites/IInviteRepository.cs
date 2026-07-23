using Korkut.Application.Common;
using Korkut.Domain.Invites;

namespace Korkut.Application.Invites;

/// <summary>
/// Persistence port for office invitations, within the owner's own tenant. Listings show
/// pending invites only, newest first; resolving a token before a session exists is a
/// different job, see <see cref="IInviteResolver"/>.
/// </summary>
public interface IInviteRepository : IRepository<Invite>;
