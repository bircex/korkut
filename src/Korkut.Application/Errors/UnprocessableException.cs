namespace Korkut.Application.Errors;

/// <summary>
/// The request was understood but its content cannot be accepted (an upload the validator
/// refused, for example). Application-layer code throws this so it stays free of any
/// web/framework type; the Web exception handler maps it to a 422 with a localized
/// <c>{ detail }</c> body — the same status the validation contract uses.
/// </summary>
public sealed class UnprocessableException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
