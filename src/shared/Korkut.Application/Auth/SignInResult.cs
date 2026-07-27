namespace Korkut.Application.Auth;

/// <summary>
/// The result of a sign-in attempt. <see cref="User"/> is set only on success, and only
/// then may a session be issued; TOTP still gates it when the account has 2FA enrolled.
/// </summary>
public sealed record SignInResult(AuthOutcome Outcome, SessionUser? User)
{
    public static readonly SignInResult InvalidCredentials = new(AuthOutcome.InvalidCredentials, null);

    public static readonly SignInResult LockedOut = new(AuthOutcome.LockedOut, null);

    public static SignInResult Success(SessionUser user) => new(AuthOutcome.Success, user);
}
