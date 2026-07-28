namespace Korkut.Contract.Agent.Chat;

public sealed record SessionBriefResponse(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);
