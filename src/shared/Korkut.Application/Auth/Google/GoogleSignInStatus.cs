namespace Korkut.Application.Auth.Google;

/// <summary>How a Google callback ended.</summary>
public enum GoogleSignInStatus
{
    /// <summary>Identity verified and linked; no second factor needed, a session may be issued.</summary>
    Authenticated,

    /// <summary>Identity verified, but Korkut TOTP must be completed before a full session.</summary>
    TwoFactorRequired,

    /// <summary>E-mail verified by Google but no Korkut account exists — eligible for self-serve signup.</summary>
    SignupEligible,

    /// <summary>Rejected — see <see cref="GoogleSignInResult.Reason"/>.</summary>
    Rejected,
}
