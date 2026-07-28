namespace Korkut.Contract.Agent.Chat;

public sealed record SessionPageResponse(IReadOnlyList<SessionBriefResponse> Items, int Total);
