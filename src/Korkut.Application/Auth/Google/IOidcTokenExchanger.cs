namespace Korkut.Application.Auth.Google;

/// <summary>Exchanges an auth code (+ PKCE verifier) for tokens at Google's token endpoint.</summary>
public interface IOidcTokenExchanger
{
    Task<string> ExchangeForIdTokenAsync(string code, string codeVerifier, CancellationToken ct = default);
}
