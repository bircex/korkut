using Korkut.Agent.Chat.Abstractions;
using Korkut.Contract.Matters;

namespace Korkut.Agent.Chat.Services;

/// <summary>
/// Who sees a conversation: a matter's conversations are its team's (and whoever sees every
/// matter), a personal one (no matter) only its owner's, whatever their role. Call it inside a
/// scope that pins the operational database.
/// </summary>
public sealed class ConversationAccess(IMatterAccess matters, ISessionService sessions, Korkut.Core.ICurrentUser user)
{
    public async Task<SessionFilter> FilterAsync(Guid officeId, Guid? matterId = null, bool personalOnly = false, CancellationToken ct = default)
    {
        var scope = await matters.ScopeAsync(officeId, ct);
        return new SessionFilter(scope.UserId, scope.AllMatters, [.. scope.MatterIds], matterId, personalOnly);
    }

    public async Task<bool> CanSeeAsync(Guid officeId, Guid? matterId, Guid? createdBy, CancellationToken ct = default)
    {
        var scope = await matters.ScopeAsync(officeId, ct);
        return matterId is { } id ? scope.CanSee(id) : createdBy == scope.UserId;
    }

    /// <summary>Whether the conversation exists and the caller may see it.</summary>
    public async Task<bool> CanSeeAsync(Guid officeId, Guid conversationId, CancellationToken ct = default)
        => await sessions.GetAsync(officeId, conversationId, ct) is { } c && await CanSeeAsync(officeId, c.MatterId, c.CreatedBy, ct);

    /// <summary>Removing a conversation is for whoever started it, or whoever sees every matter.</summary>
    public async Task<bool> CanRemoveAsync(Guid officeId, Guid conversationId, CancellationToken ct = default)
        => await sessions.GetAsync(officeId, conversationId, ct) is { } c
            && await CanSeeAsync(officeId, c.MatterId, c.CreatedBy, ct)
            && (c.CreatedBy == user.UserId || user.Permissions.Contains(Korkut.Core.Permissions.MattersViewAll));
}
