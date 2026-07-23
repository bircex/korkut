namespace Korkut.Application.Auth.Google;

/// <summary>Verified claims extracted from a Google ID token.</summary>
public sealed record GoogleIdentity(string Subject, string Email, bool EmailVerified, string Nonce);
