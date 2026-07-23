namespace Korkut.Application.Offices.Responses;

/// <summary>An office as the platform-admin console lists it.</summary>
public sealed record OfficeResponse(Guid Id, string Name, bool Disabled, bool RequireTwoFactor, DateTimeOffset CreatedAt);
