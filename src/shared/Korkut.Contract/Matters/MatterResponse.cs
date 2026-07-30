namespace Korkut.Contract.Matters;

/// <summary>Transport shape for a matter in list/create results. <see cref="ClientName"/> and
/// <see cref="Court"/> are the free-text values captured when the matter was opened.</summary>
public sealed record MatterResponse(
    Guid Id, string Title, string? ClientName, string? FileNo, string? Court, string? CaseType,
    string? Counterparty, string Status, DateTimeOffset? OpenedAt, DateTimeOffset? UpdatedAt);
