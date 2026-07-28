namespace Korkut.Contract.Agent.Drafting;

public sealed record PendingEditResponse(Guid Id, string ContentMd, string? Summary);
