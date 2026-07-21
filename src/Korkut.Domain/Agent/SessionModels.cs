namespace Korkut.Domain.Agent;

/// <summary>An assistant conversation ("session") — domain model.</summary>
public sealed record Conversation(
    Guid Id, Guid OfficeId, string Title, Guid? MatterId, Guid? CreatedBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>A conversation summary with its author resolved to a display value (list read model).</summary>
public sealed record SessionBrief(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);

/// <summary>One turn (message) in a conversation — domain model.</summary>
public sealed record ChatTurn(Guid Id, Guid ConversationId, string Role, string Content, DateTimeOffset CreatedAt);

/// <summary>The default placeholder title a fresh conversation gets until auto-titled.</summary>
public static class Session
{
    public const string DefaultTitle = "Yeni görüşme";
}
