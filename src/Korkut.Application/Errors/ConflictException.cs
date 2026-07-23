namespace Korkut.Application.Errors;

/// <summary>
/// The request is well-formed but would break an invariant the office depends on (for
/// example removing its last owner). Application-layer code throws this so it stays free
/// of any web/framework type; the Web exception handler maps it to a 409 with a localized
/// <c>{ detail }</c> body.
/// </summary>
public sealed class ConflictException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
