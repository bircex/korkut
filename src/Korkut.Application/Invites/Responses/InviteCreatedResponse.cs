namespace Korkut.Application.Invites.Responses;

/// <summary>
/// A freshly created invitation. This is the ONLY time the raw token is returned — it is
/// stored hashed — so the caller must build the invite link from this response.
/// </summary>
public sealed record InviteCreatedResponse(
    Guid Id, string Email, string Role, string Token, DateTimeOffset ExpiresAt);
