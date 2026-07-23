namespace Korkut.Application.OfficeCore.Clients.Responses;

/// <summary>Transport shape for a client. Serialized snake_case by the global JSON contract.</summary>
public sealed record ClientResponse(
    Guid Id, string Name, string Kind, string? Email, string? Phone, DateTimeOffset CreatedAt);
