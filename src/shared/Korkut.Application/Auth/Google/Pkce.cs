using System.Security.Cryptography;
using System.Text;

namespace Korkut.Application.Auth.Google;

/// <summary>PKCE S256 code-challenge derivation (RFC 7636).</summary>
public static class Pkce
{
    public static string S256(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return OidcChallenge.Base64Url(hash);
    }
}
