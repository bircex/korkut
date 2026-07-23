namespace Korkut.Application.Channels.Responses;

/// <summary>
/// The e-mail channel as the settings screen sees it: the stored password is reported only
/// as <c>HasPassword</c>, never returned.
/// </summary>
public sealed record EmailChannelResponse(
    string Kind, bool Enabled, string FromAddress, string FromName, bool HasPassword, bool CanEdit);
