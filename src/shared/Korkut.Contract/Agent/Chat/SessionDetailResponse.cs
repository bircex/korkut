namespace Korkut.Contract.Agent.Chat;

public sealed record SessionDetailResponse(
    Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt, IReadOnlyList<ChatMessageResponse> Messages);
