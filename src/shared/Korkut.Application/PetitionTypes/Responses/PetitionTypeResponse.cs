namespace Korkut.Application.PetitionTypes.Responses;

/// <summary>One petition type from the embedded catalog.</summary>
public sealed record PetitionTypeResponse(string Id, string Ad, string? Mahkeme, bool Sik);
