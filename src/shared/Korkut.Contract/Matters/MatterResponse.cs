namespace Korkut.Contract.Matters;

/// <summary>Transport shape for a matter in list/create results. <see cref="ClientName"/> is the
/// name snapshotted from the linked client at bind time; <see cref="Court"/> may still be a
/// free-text value or a snapshot from a linked court.</summary>
public sealed record MatterResponse(
    Guid Id, string Title, string? ClientName, string? FileNo, string? Court, string? CaseType,
    string? Counterparty, string Status, DateTimeOffset? OpenedAt, DateTimeOffset? UpdatedAt);
