namespace Korkut.Application.Errors;

/// <summary>
/// The caller is authenticated and permitted to use the endpoint, but not to do this
/// particular thing (for example removing their own membership). Application-layer code
/// throws this so it stays free of any web/framework type; the Web exception handler maps
/// it to a 403 with a localized <c>{ detail }</c> body.
/// </summary>
public sealed class ForbiddenException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
