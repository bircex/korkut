using System.Security.Cryptography;
using System.Text;
using Korkut.SharedKernel.Time;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korkut.Infrastructure.Security;

/// <summary>A verified-but-office-less Google identity captured for self-serve signup.</summary>
public sealed record SignupTicket(string Email, string GoogleSub);

/// <summary>
/// A short-lived HS256 "signup ticket": proof that Google verified an email that has no
/// Korkut account yet. Minted by the OAuth callback (own audience, so it can never act
/// as an access token) and redeemed by <c>/api/demo-request</c> to provision a pending
/// office. Signed with KORKUT_SECRET; carries the verified email + provider subject.
/// </summary>
public sealed class SignupTicketService
{
    public const string Audience = "korkut-signup";

    private readonly SymmetricSecurityKey _key;
    private readonly IClock _clock;
    private readonly JsonWebTokenHandler _handler = new();

    public SignupTicketService(string secret, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException("KORKUT_SECRET must be set and at least 32 bytes.");
        }

        _key = new SymmetricSecurityKey(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        _clock = clock;
    }

    public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(15);

    public string Mint(SignupTicket ticket)
    {
        var now = _clock.UtcNow.UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AccessTokenService.Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + Lifetime,
            Claims = new Dictionary<string, object>
            {
                ["email"] = ticket.Email,
                ["gsub"] = ticket.GoogleSub,
            },
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        });
    }

    public async Task<SignupTicket?> ValidateAsync(string token)
    {
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = AccessTokenService.Issuer,
            ValidAudience = Audience,
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
        return new SignupTicket((string)c["email"], (string)c["gsub"]);
    }
}
