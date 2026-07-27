namespace Korkut.Application.Auth.Requests;

/// <summary>
/// Body for the second sign-in step: either the authenticator code or a recovery code.
/// Neither is required by itself, but one of them has to be there.
/// </summary>
public sealed record TotpRequest
{
    public string? Code { get; init; }

    public string? RecoveryCode { get; init; }
}
