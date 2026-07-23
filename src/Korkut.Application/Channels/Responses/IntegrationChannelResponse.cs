namespace Korkut.Application.Channels.Responses;

/// <summary>One row of the integrations screen; reported for every kind, configured or not.</summary>
public sealed record IntegrationChannelResponse(
    string Kind, bool Enabled, bool Configured, string BaseUrl, bool Linked, string LinkedId, bool CanEdit);
