namespace Korkut.Application.Settings.Responses;

/// <summary>One drafting-style layer as the client sees it.</summary>
public sealed record StyleFieldsResponse(string Letterhead, string Signature, string Tone, string Boilerplate);
