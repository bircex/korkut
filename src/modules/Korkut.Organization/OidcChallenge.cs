using Korkut.Organization.Entities;
using System.Security.Cryptography;

namespace Korkut.Organization;

/// <summary>The per-login secrets: CSRF state, replay-guard nonce, and the PKCE pair.</summary>
public sealed record OidcChallenge(string State, string Nonce, string CodeVerifier, string CodeChallenge)
{
    public static OidcChallenge Create()
    {
        var state = Rand();
        var nonce = Rand();
        var verifier = Rand();
        return new OidcChallenge(state, nonce, verifier, Pkce.S256(verifier));
    }

    private static string Rand() => Base64Url(RandomNumberGenerator.GetBytes(32));

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
