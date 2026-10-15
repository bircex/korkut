namespace Korkut.Tools.Core;

/// <summary>
/// The trusted identity a tool call runs under: the office (tenant) it is pinned to, the acting
/// user, and that user's effective permission set (for tool RBAC). Built from the access token at
/// the edge — never from a request body. <see cref="ConversationId"/> is the chat conversation the
/// turn belongs to (null outside a conversation, e.g. one-shot brain features) — conversation-bound
/// tools such as document drafting need it. <see cref="MatterId"/> is the matter that conversation
/// belongs to: tools then work on that matter only.
/// </summary>
public sealed record AgentToolContext(
    Guid OfficeId, Guid UserId, IReadOnlySet<string> Permissions, Guid? ConversationId = null, Guid? MatterId = null)
{
    /// <summary>True when the tool needs no permission, or the caller holds the required one.</summary>
    public bool Can(string? permission) => permission is null || Permissions.Contains(permission);
}
