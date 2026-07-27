namespace Korkut.Core;

/// <summary>
/// A requested resource does not exist (or is not visible to the caller's tenant).
/// Application-layer code throws this so it stays free of any web/framework type; the
/// Web exception handler maps it to a 404 with a localized <c>{ detail }</c> body.
/// </summary>
public sealed class NotFoundException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
