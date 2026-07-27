using Korkut.Contract.Organization;
namespace Korkut.Application.Auth.Google;

/// <summary>
/// The outcome of a Google callback. <see cref="User"/> is present exactly when an identity
/// resolved to an account, and even then a session may only be issued once
/// <see cref="Status"/> says the second factor is not pending.
/// </summary>
public sealed record GoogleSignInResult(
    GoogleSignInStatus Status, SessionUser? User, string? Reason, string? Email = null, string? GoogleSub = null)
{
    public static GoogleSignInResult Reject(string reason) => new(GoogleSignInStatus.Rejected, null, reason);

    /// <summary>A verified e-mail with no account behind it: the self-serve signup path.</summary>
    public static GoogleSignInResult Signup(string email, string googleSub) =>
        new(GoogleSignInStatus.SignupEligible, null, null, email, googleSub);

    public static GoogleSignInResult SignedIn(SessionUser user, bool twoFactorRequired) =>
        new(twoFactorRequired ? GoogleSignInStatus.TwoFactorRequired : GoogleSignInStatus.Authenticated, user, null);
}
