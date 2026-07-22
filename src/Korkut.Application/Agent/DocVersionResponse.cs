namespace Korkut.Agent.Contracts.Documents;

public sealed record DocVersionResponse(Guid Id, string? Author, string? Note, DateTimeOffset CreatedAt);
