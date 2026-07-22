using System.Security.Cryptography;
using System.Text;
using Korkut.Domain.Time;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korkut.Infrastructure.Security;

/// <summary>Minimal claims carried in a Korkut access token — no PII/email.</summary>
public sealed record AccessClaims(Guid UserId, Guid OfficeId, string Role, string SecurityStamp);

/// <summary>
/// Short-lived HS256 access tokens signed with KORKUT_SECRET. The payload carries
/// only sub/oid/role and the security stamp; a stamp mismatch against the current
/// user (credential/role/tenant change) makes an outstanding token stale, so
/// authorization can never lag a change (checked per request by the caller).
/// </summary>
public sealed class AccessTokenService
{
    public const string Issuer = "korkut";
    public const string OfficeAudience = "korkut-office";
    public const string AdminAudience = "korkut-admin";

    private readonly SymmetricSecurityKey _key;
    private readonly IClock _clock;
    private readonly string _audience;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessTokenService(string secret, IClock clock, string audience = OfficeAudience)
    {
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            // Fail closed: no silent random fallback for a missing/weak signing secret.
            throw new InvalidOperationException("KORKUT_SECRET must be set and at least 32 bytes.");
        }

        _key = new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        _clock = clock;
        _audience = audience;
    }

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(15);

    public string Mint(AccessClaims claims)
    {
        var now = _clock.UtcNow.UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = _audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + Lifetime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = claims.UserId.ToString(),
                ["oid"] = claims.OfficeId.ToString(),
                ["role"] = claims.Role,
                ["stmp"] = claims.SecurityStamp,
            },
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        };
        return _handler.CreateToken(descriptor);
    }

    public async Task<AccessClaims?> ValidateAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = _audience,
            IssuerSigningKey = _key,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        });
        if (!result.IsValid)
        {
            return null;
        }

        var c = result.Claims;
        return new AccessClaims(
            Guid.Parse((string)c["sub"]), Guid.Parse((string)c["oid"]), (string)c["role"], (string)c["stmp"]);
    }
}
