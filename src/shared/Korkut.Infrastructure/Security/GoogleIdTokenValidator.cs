using Korkut.Organization;
using Korkut.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korkut.Infrastructure.Security;

/// <summary>
/// Validates a Google ID token's RS256 signature and standard claims (iss, aud,
/// exp) and extracts sub/email/email_verified/nonce. Signing keys are injected: in
/// production the Google JWKS, in tests a local test key — so the crypto path is
/// exercised without a live Google. No primitive is implemented here.
/// </summary>
public sealed class GoogleIdTokenValidator(
    string audience,
    Func<IEnumerable<SecurityKey>> signingKeys) : IGoogleIdTokenValidator
{
    private readonly JsonWebTokenHandler _handler = new();

    public async Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidAudience = audience,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = signingKeys(),
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        var result = await _handler.ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid)
        {
            throw new SecurityTokenException("Invalid Google ID token.", result.Exception);
        }

        var claims = result.Claims;
        var sub = GetString(claims, "sub") ?? throw new SecurityTokenException("missing sub");
        var email = GetString(claims, "email") ?? throw new SecurityTokenException("missing email");
        var emailVerified = GetBool(claims, "email_verified");
        var nonce = GetString(claims, "nonce") ?? string.Empty;
        return new GoogleIdentity(sub, email, emailVerified, nonce);
    }

    private static string? GetString(IDictionary<string, object> claims, string key) =>
        claims.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static bool GetBool(IDictionary<string, object> claims, string key) =>
        claims.TryGetValue(key, out var v)
        && (v is bool b ? b : bool.TryParse(v?.ToString(), out var p) && p);
}
