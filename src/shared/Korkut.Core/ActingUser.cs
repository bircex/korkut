namespace Korkut.Core;

/// <summary>
/// The member a piece of work runs as when there is no HTTP request to name one: a message that
/// arrived from Telegram, for example, runs a chat turn as the member who linked that chat.
/// Scoped, so it lasts as long as the DI scope the work runs in. Once set, <see cref="ICurrentUser"/>
/// answers with this member, and every application service sees them exactly as it would a
/// signed-in caller. Set it only from a trusted, already-authenticated source.
/// </summary>
public sealed class ActingUser
{
    // Singletons cannot see the scope, so the member also travels with the async flow that set it
    // (and only downstream of it: an async method's changes never leak back to its caller).
    private static readonly AsyncLocal<Member?> AmbientMember = new();

    public ICurrentUser? Current { get; private set; }

    /// <summary>The member the current async flow acts as, for singletons; null in a normal request.</summary>
    public static ActingMember? Ambient => AmbientMember.Value is { } m ? new ActingMember(m.OfficeId, m.UserId, m.Role) : null;

    public void Set(Guid officeId, Guid userId, string role, IReadOnlySet<string> permissions)
    {
        if (Current is not null)
        {
            throw new InvalidOperationException("This scope already acts as a member.");
        }

        var member = new Member(officeId, userId, role, permissions);
        Current = member;
        AmbientMember.Value = member;
    }

    private sealed record Member(Guid OfficeId, Guid UserId, string Role, IReadOnlySet<string> Permissions) : ICurrentUser;
}

/// <summary>Who an out-of-request flow acts as, with the role their permissions come from.</summary>
public sealed record ActingMember(Guid OfficeId, Guid UserId, string Role);

/// <summary>
/// A short-lived bearer token for the member the current flow acts as (<see cref="ActingUser.Ambient"/>),
/// for internal services that authenticate the caller themselves, such as OCR. Null outside such a flow.
/// </summary>
public interface IActingUserCredential
{
    string? BearerToken();
}
