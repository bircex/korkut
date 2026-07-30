namespace Korkut.Contract.Agent.Drafting;

/// <summary>A drafted document in the office's document list (the UI's DocBrief). <see cref="Kind"/>
/// is the document kind ("dilekce"/"sozlesme") when known, else null.</summary>
public sealed record DocBriefResponse(
    Guid Id, Guid ConversationId, string Title, string? Kind, DateTimeOffset? UpdatedAt);
