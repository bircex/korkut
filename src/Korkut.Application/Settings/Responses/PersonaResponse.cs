namespace Korkut.Application.Settings.Responses;

/// <summary>The office persona, with whether this caller may change it.</summary>
public sealed record PersonaResponse(string Tone, string Expertise, string ExtraRules, bool CanEdit);
