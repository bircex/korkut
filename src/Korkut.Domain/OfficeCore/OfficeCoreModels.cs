namespace Korkut.Domain.OfficeCore;

/// <summary>
/// A tenant-owned case file / dosya (domain model). The court title is snapshotted onto
/// the matter at link time (<see cref="CourtNameSnapshot"/>) so a later court rename never
/// rewrites the matter's historical text.
/// </summary>
public sealed record Matter(
    Guid Id, Guid OfficeId, string Title, Guid? ClientId, string? FileNo, string? CaseType,
    string? Counterparty, Guid? CourtId, string? CourtNameSnapshot, string Status,
    DateTimeOffset? OpenedAt, DateTimeOffset? DeletedAt, DateTimeOffset CreatedAt);

/// <summary>A hearing / duruşma on a matter's calendar (domain model).</summary>
public sealed record Hearing(
    Guid Id, Guid OfficeId, Guid MatterId, DateTimeOffset ScheduledAt, string? Type,
    string? Location, string? Result, string? Notes, string ReminderStatus);
