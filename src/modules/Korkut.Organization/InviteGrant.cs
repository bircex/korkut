using Korkut.Organization.Entities;
namespace Korkut.Organization;

/// <summary>
/// What a pending invite token grants, resolved before any session exists — which office,
/// which e-mail, which role. Carries no token material.
/// </summary>
public sealed record InviteGrant(Guid Id, Guid OfficeId, string Email, string Role);
