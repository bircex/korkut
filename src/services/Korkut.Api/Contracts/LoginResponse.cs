namespace Korkut.Api.Contracts;

/// <summary>
/// The result of the password step. When TOTP is required the caller holds a short-lived
/// pending cookie and must post the second factor before it gets a real session.
/// </summary>
public sealed record LoginResponse(bool TotpRequired);
