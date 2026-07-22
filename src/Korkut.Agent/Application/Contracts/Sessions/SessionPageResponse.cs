namespace Korkut.Agent.Application.Contracts.Sessions;

public sealed record SessionPageResponse(IReadOnlyList<SessionBriefResponse> Items, int Total);
