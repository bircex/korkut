namespace Korkut.Contract.Notes;

/// <summary>Transport shape for a communication note.</summary>
public sealed record NoteResponse(
    Guid Id, string Body, string Channel, Guid? MatterId, Guid? ClientId,
    DateTimeOffset OccurredAt, DateTimeOffset CreatedAt);
