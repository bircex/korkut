using Korkut.Core;

namespace Korkut.Organization;

/// <summary>
/// Persistence port for office invitations, within the owner's own tenant. Listings show pending
/// invites only, newest first; resolving a token before a session exists is a different job.
/// </summary>
public interface IInviteRepository : IRepository<Invite>;
