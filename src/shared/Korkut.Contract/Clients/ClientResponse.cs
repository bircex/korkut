namespace Korkut.Contract.Clients;

/// <summary>Transport shape for a client. Serialized snake_case by the global JSON contract.</summary>
public sealed record ClientResponse(
    Guid Id,
    string FullName,
    string Kind,
    string? Email,
    string? Phone,
    string? NationalId,
    string? Address,
    DateOnly? DateOfBirth,
    DateTimeOffset CreatedAt);
