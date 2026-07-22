namespace Korkut.Agent.Domain;

/// <summary>
/// A conversation summary (list read model). Carries the raw <see cref="CreatedBy"/> id —
/// the author's display name lives in the operational DB, so the Agent host resolves it via
/// an operational lookup when building the response (the Agent DB never joins operational data).
/// </summary>
public sealed record SessionBrief(Guid Id, string Title, Guid? MatterId, Guid? CreatedBy, DateTimeOffset? UpdatedAt);

/// <summary>One turn (message) in a conversation — domain model.</summary>
public sealed record ChatTurn(Guid Id, Guid ConversationId, string Role, string Content, DateTimeOffset CreatedAt);

/// <summary>The default placeholder title a fresh conversation gets until auto-titled.</summary>
public static class Session
{
    public const string DefaultTitle = "Yeni görüşme";
}
