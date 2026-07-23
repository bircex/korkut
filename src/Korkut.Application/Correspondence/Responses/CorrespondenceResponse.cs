namespace Korkut.Application.Correspondence.Responses;

/// <summary>Transport shape for a correspondence-register entry.</summary>
public sealed record CorrespondenceResponse(
    Guid Id, Guid? MatterId, string Direction, string Title, string? ReferenceNo,
    DateOnly? DocDate, string? Notes, DateTimeOffset CreatedAt);
