namespace Korkut.Application.Channels.Responses;

/// <summary>Every channel kind, in a stable order, with its configuration and pairing state.</summary>
public sealed record IntegrationsResponse(IReadOnlyList<IntegrationChannelResponse> Channels);
