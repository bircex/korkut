using Korkut.Application.Auth.Requests;

namespace Korkut.Application.Auth;

/// <summary>
/// Local sign-in, lockout and password recovery. Every method here is reachable without a
/// session, so all of them are written to give nothing away: a wrong password and an
/// unknown account are the same answer, and asking for a reset link says nothing about
/// whether the account exists.
/// </summary>
public interface IAuthService
{
    /// <summary>Verifies the credentials and reports what a session may be issued for.</summary>
    Task<SignInResult> SignInAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// The session owner's current role and stamp, for minting a token after a step the
    /// caller has already passed (TOTP, refresh). Null if the account is gone.
    /// </summary>
    Task<SessionUser?> FindSessionUserAsync(Guid officeId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Completes the second step for a caller who already passed the password step. Null
    /// when the code is wrong, so a session is never issued on a failed challenge.
    /// </summary>
    Task<SessionUser?> CompleteTwoFactorAsync(Guid officeId, Guid userId, TotpRequest request, CancellationToken ct = default);

    /// <summary>Always succeeds from the caller's view — it must not reveal account existence.</summary>
    Task RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken ct = default);

    Task<ResetOutcome> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
