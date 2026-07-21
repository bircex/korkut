namespace Korkut.Domain.Correspondence;

/// <summary>
/// A correspondence-register ("evrak") entry (domain model): an inbound, outbound, or
/// internal official document logged against a matter. Metadata only — distinct from a
/// stored file.
/// </summary>
public sealed record CorrespondenceItem(
    Guid Id,
    Guid OfficeId,
    Guid? MatterId,
    string Direction,
    string Title,
    string? ReferenceNo,
    DateOnly? DocDate,
    string? Notes,
    DateTimeOffset CreatedAt);
