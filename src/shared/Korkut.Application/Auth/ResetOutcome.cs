namespace Korkut.Application.Auth;

/// <summary>How a password reset ended.</summary>
public enum ResetOutcome
{
    Success,
    InvalidOrExpiredToken,
}
