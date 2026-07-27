namespace Korkut.Contract;

/// <summary>
/// The outcome kind a service reports. The host maps each to an HTTP status; nothing below the
/// host layer knows about HTTP, so a service stays framework-free while still expressing intent.
/// </summary>
public enum ResultStatus
{
    Ok,
    NotFound,
    BadRequest,
    Conflict,
    Forbidden,
    Unauthorized,
    Unprocessable,
}
