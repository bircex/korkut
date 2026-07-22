namespace Korkut.Agent.Contracts.Sessions;

public sealed record SessionBriefResponse(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);
