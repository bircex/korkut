namespace Korkut.Agent.Domain;

/// <summary>An assistant conversation ("session") — domain model.</summary>
public sealed record Conversation(
    Guid Id, Guid OfficeId, string Title, Guid? MatterId, Guid? CreatedBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
