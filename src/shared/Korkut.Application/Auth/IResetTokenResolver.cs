using Korkut.Organization;

namespace Korkut.Application.Auth;

/// <summary>
/// Resolves which office a password-reset token belongs to before any session exists.
/// Null for an unknown, expired or already-used token — the three must look the same.
/// </summary>
public interface IResetTokenResolver
{
    Task<ResetTokenOwner?> ResolveAsync(string tokenHash, CancellationToken ct = default);
}
