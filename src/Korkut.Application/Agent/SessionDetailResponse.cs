namespace Korkut.Agent.Contracts.Sessions;

public sealed record SessionDetailResponse(
    Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt, IReadOnlyList<ChatMessageResponse> Messages);
