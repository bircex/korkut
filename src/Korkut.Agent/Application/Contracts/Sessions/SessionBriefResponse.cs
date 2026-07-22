namespace Korkut.Agent.Application.Contracts.Sessions;

public sealed record SessionBriefResponse(Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt);
