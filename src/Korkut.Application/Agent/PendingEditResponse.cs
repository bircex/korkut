namespace Korkut.Agent.Contracts.Documents;

public sealed record PendingEditResponse(Guid Id, string ContentMd, string? Summary);
