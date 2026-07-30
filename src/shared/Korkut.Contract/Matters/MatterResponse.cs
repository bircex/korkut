namespace Korkut.Contract.Matters;

/// <summary>Transport shape for a matter in list/create results.</summary>
public sealed record MatterResponse(
    Guid Id, string Title, Guid? ClientId, string Status, string? CourtNameSnapshot, string? FileNo, string? CaseType, string? Counterparty, DateTimeOffset CreatedAt);
