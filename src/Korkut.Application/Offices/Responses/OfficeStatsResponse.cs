namespace Korkut.Application.Offices.Responses;

/// <summary>Platform-wide office counts for the admin console.</summary>
public sealed record OfficeStatsResponse(int Offices, int ActiveOffices, int DisabledOffices);
