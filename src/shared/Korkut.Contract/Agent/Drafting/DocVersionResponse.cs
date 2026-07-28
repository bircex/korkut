namespace Korkut.Contract.Agent.Drafting;

public sealed record DocVersionResponse(Guid Id, string? Author, string? Note, DateTimeOffset CreatedAt);
