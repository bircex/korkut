namespace Korkut.Application.Auth;

/// <summary>
/// The second factor at sign-in: a TOTP code, or one of the printed recovery codes. Both
/// are single-use — an accepted TOTP step and a used recovery code cannot be replayed.
/// </summary>
public interface ITwoFactorChallenge
{
    /// <summary>True when the code is valid for this user right now.</summary>
    Task<bool> VerifyCodeAsync(Guid officeId, Guid userId, string code, CancellationToken ct = default);

    /// <summary>Spends a recovery code; false if it is unknown or already spent.</summary>
    Task<bool> UseRecoveryCodeAsync(Guid officeId, Guid userId, string rawCode, CancellationToken ct = default);
}
