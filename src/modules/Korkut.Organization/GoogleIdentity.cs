using Korkut.Organization.Entities;
namespace Korkut.Organization;

/// <summary>Verified claims extracted from a Google ID token.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string Nonce);
