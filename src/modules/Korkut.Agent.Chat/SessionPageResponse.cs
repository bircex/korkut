namespace Korkut.Agent.Chat;

public sealed record SessionPageResponse(IReadOnlyList<SessionBriefResponse> Items, int Total);
