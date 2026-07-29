namespace Korkut.Core;

/// <summary>
/// The caller's request is malformed or violates a validation rule. Application-layer
/// code throws this so it stays free of any web/framework type; the Web exception
/// handler maps it to a 400 with a localized <c>{ detail }</c> body.
/// </summary>
public sealed class BadRequestException(string detail) : Exception(detail)
{
    public string Detail { get; } = detail;
}
