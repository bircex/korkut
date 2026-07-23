namespace Korkut.Application.Auth;

/// <summary>How a sign-in attempt ended.</summary>
public enum AuthOutcome
{
    Success,
    InvalidCredentials,
    LockedOut,
}
