namespace Korkut.Agent.Application.Contracts.Sessions;

public sealed record SessionDetailResponse(
    Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt, IReadOnlyList<ChatMessageResponse> Messages);
