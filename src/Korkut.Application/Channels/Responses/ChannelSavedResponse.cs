namespace Korkut.Application.Channels.Responses;

/// <summary>Acknowledgement for a token-only channel save: saved, and whether a secret is now stored.</summary>
public sealed record ChannelSavedResponse(bool Ok, bool Configured);
