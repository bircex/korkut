namespace Korkut.Application.Invites.Responses;

/// <summary>A pending invitation in an owner's listing. Carries no token material.</summary>
public sealed record InviteResponse(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);
