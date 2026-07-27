namespace Korkut.Contract.Channels;

/// <summary>Acknowledgement for a token-only channel save: saved, and whether a secret is now stored.</summary>
public sealed record ChannelSavedResponse(bool Ok, bool Configured);

/// <summary>
/// The e-mail channel as the settings screen sees it: the stored password is reported only as
/// <c>HasPassword</c>, never returned.
/// </summary>
public sealed record EmailChannelResponse(
    string Kind, bool Enabled, string FromAddress, string FromName, bool HasPassword, bool CanEdit);

/// <summary>One row of the integrations screen; reported for every kind, configured or not.</summary>
public sealed record IntegrationChannelResponse(
    string Kind, bool Enabled, bool Configured, string BaseUrl, bool Linked, string LinkedId, bool CanEdit);

/// <summary>Every channel kind, in a stable order, with its configuration and pairing state.</summary>
public sealed record IntegrationsResponse(IReadOnlyList<IntegrationChannelResponse> Channels);

/// <summary>A freshly minted one-time pairing code for a channel kind.</summary>
public sealed record LinkCodeResponse(string Code, string Kind);
