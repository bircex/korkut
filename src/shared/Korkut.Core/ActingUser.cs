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
    public ICurrentUser? Current { get; private set; }

    public void Set(Guid officeId, Guid userId, IReadOnlySet<string> permissions)
    {
        if (Current is not null)
        {
            throw new InvalidOperationException("This scope already acts as a member.");
        }

        Current = new Member(officeId, userId, permissions);
    }

    private sealed record Member(Guid OfficeId, Guid UserId, IReadOnlySet<string> Permissions) : ICurrentUser;
}
