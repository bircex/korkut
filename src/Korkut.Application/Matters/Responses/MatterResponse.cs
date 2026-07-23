namespace Korkut.Application.Matters.Responses;

/// <summary>Transport shape for a matter in list/create results.</summary>
public sealed record MatterResponse(
    Guid Id, string Title, Guid? ClientId, string Status, string? CourtNameSnapshot, DateTimeOffset CreatedAt);
