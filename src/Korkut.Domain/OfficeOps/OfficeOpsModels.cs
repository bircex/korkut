namespace Korkut.Domain.OfficeOps;

/// <summary>A tenant-owned task (domain model). Named WorkTask to avoid clashing with System.Threading.Tasks.Task.</summary>
public sealed record WorkTask(
    Guid Id, Guid OfficeId, Guid? MatterId, Guid? AssigneeId, string Title, string Status,
    DateTimeOffset? DueAt, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);

/// <summary>
/// A statutory or internal deadline (domain model). The due value is a BUSINESS DATE in
/// an explicit IANA time zone, not a bare instant.
/// </summary>
public sealed record Deadline(
    Guid Id, Guid OfficeId, Guid? MatterId, string Kind, string Title, DateOnly DueDate,
    string TimeZoneId, int ReminderDaysBefore, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt);

/// <summary>An immutable communication note (call/email/meeting log) on a matter/client.</summary>
public sealed record CommunicationNote(
    Guid Id, Guid OfficeId, Guid? MatterId, Guid? ClientId, string Channel, string Body,
    DateTimeOffset OccurredAt, DateTimeOffset CreatedAt);
