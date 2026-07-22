using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Presentation.Contracts.Sessions;

public sealed record SessionBriefResponse(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);

public sealed record ChatMessageResponse(string Role, string Content);

public sealed record SessionDetailResponse(
    Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt, IReadOnlyList<ChatMessageResponse> Messages);

public sealed record SessionPageResponse(IReadOnlyList<SessionBriefResponse> Items, int Total);

public sealed record RenameSessionRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }
}

public sealed record ChatRequest
{
    [Required, MaxLength(40000)]
    public string? Text { get; init; }
}

public sealed record ChatResponse(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);
