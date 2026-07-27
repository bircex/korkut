using Korkut.Application.Auth.Google;
using System.Text.Json;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// Exchanges a Google authorization code (+ PKCE verifier) for tokens at Google's
/// token endpoint over HTTPS (KDM-016). Returns the raw ID token; the caller verifies
/// its signature/claims separately. The client secret is sent only in this
/// server-to-server call, never to the browser.
/// </summary>
public sealed class GoogleTokenExchanger(HttpClient http, GoogleOidcOptions options) : IOidcTokenExchanger
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public async Task<string> ExchangeForIdTokenAsync(string code, string codeVerifier, CancellationToken ct = default)
    {
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["redirect_uri"] = options.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier,
        });

        using var response = await http.PostAsync(TokenEndpoint, body, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.TryGetProperty("id_token", out var idToken)
            ? idToken.GetString() ?? throw new InvalidOperationException("Google token response had no id_token.")
            : throw new InvalidOperationException("Google token response had no id_token.");
    }
}
