namespace Korkut.Application.Common;

/// <summary>
/// The authenticated caller, resolved from the trusted access token only (never a DTO,
/// query parameter or route value). Application services read the tenant from here, so a
/// use-case can never be pointed at another office by request data.
/// </summary>
public interface ICurrentUser
{
    Guid OfficeId { get; }

    Guid UserId { get; }

    IReadOnlySet<string> Permissions { get; }
}
