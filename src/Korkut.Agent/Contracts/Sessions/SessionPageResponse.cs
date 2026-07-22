namespace Korkut.Agent.Contracts.Sessions;

public sealed record SessionPageResponse(IReadOnlyList<SessionBriefResponse> Items, int Total);
