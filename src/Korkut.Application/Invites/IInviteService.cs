using Korkut.Application.Invites.Requests;
using Korkut.Application.Invites.Responses;

namespace Korkut.Application.Invites;

/// <summary>
/// Office-invitation use-cases. An owner creates, lists and revokes invites inside their
/// own office; accepting one is anonymous — the token itself says which office it belongs
/// to, so the accept flow works before the invitee has any session.
/// </summary>
public interface IInviteService
{
    Task<IReadOnlyList<InviteResponse>> ListAsync(CancellationToken ct = default);

    /// <summary>Mints the invitation; the raw token comes back once, in the response.</summary>
    Task<InviteCreatedResponse> CreateAsync(CreateInviteRequest request, CancellationToken ct = default);

    Task RevokeAsync(Guid inviteId, CancellationToken ct = default);

    /// <summary>Consumes the invite and makes sure the invitee exists as a member of that office.</summary>
    Task<InviteAcceptedResponse> AcceptAsync(AcceptInviteRequest request, CancellationToken ct = default);
}
