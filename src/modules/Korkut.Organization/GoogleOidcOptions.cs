namespace Korkut.Organization;

/// <summary>
/// Google OIDC configuration, bound to the exact environment names the deployment already
/// uses (GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET / GOOGLE_REDIRECT_URI). Missing values are
/// not a crash: the flow simply reports itself unconfigured.
/// </summary>
public sealed class GoogleOidcOptions
{
    public const string GoogleIssuer = "https://accounts.google.com";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RedirectUri { get; init; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(RedirectUri);
}
