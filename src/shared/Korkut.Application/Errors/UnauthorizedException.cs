namespace Korkut.Application.Errors;

/// <summary>
/// The caller's session no longer corresponds to anyone (the account was removed while the
/// token was still valid). Application-layer code throws this so it stays free of any
/// web/framework type; the Web exception handler maps it to a 401 with a localized
/// <c>{ detail }</c> body.
/// </summary>
public sealed class UnauthorizedException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
