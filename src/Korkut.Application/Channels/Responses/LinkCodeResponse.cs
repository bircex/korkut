namespace Korkut.Application.Channels.Responses;

/// <summary>A freshly minted one-time pairing code for a channel kind.</summary>
public sealed record LinkCodeResponse(string Code, string Kind);
