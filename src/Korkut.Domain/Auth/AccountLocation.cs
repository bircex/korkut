namespace Korkut.Domain.Auth;

/// <summary>
/// Where an account lives, resolved from an e-mail before any session exists. Sign-in has
/// to find the office first: the caller never says which one they belong to.
/// </summary>
public sealed record AccountLocation(Guid UserId, Guid OfficeId, string Email);
