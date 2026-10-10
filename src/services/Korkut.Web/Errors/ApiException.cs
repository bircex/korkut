namespace Korkut.Web.Errors;

/// <summary>
/// An expected, client-facing failure carrying an HTTP status and a localized
/// message. Serialized as the Python-compatible <c>{ "detail": "&lt;message&gt;" }</c>
/// body by <see cref="ApiExceptionHandler"/>. Use the factory helpers so status
/// codes stay consistent with the parity contract (401/403/404/400/409/503).
/// </summary>
public sealed class ApiException(int statusCode, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Detail { get; } = detail;

    public static ApiException Unauthorized(string detail = "Oturum gerekli") =>
        new(StatusCodes.Status401Unauthorized, detail);

    public static ApiException Forbidden(string detail) =>
        new(StatusCodes.Status403Forbidden, detail);

    public static ApiException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, detail);

    public static ApiException BadRequest(string detail) =>
        new(StatusCodes.Status400BadRequest, detail);

    public static ApiException Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, detail);

    /// <summary>503: a dependency the request needs (such as the model gateway) is unavailable.</summary>
    public static ApiException ServiceUnavailable(string detail) =>
        new(StatusCodes.Status503ServiceUnavailable, detail);

    /// <summary>423: the account is temporarily locked out after too many failed attempts.</summary>
    public static ApiException Locked(string detail) =>
        new(StatusCodes.Status423Locked, detail);
}
