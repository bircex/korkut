namespace Korkut.Agent.Drafting;

public sealed record PendingEditResponse(Guid Id, string ContentMd, string? Summary);
