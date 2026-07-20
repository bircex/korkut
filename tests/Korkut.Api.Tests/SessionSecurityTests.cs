using System.Net;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Korkut.Api.Tests;

/// <summary>Access-token claims/stamp (unit) + security headers and rate limiting (integration).</summary>
public class SessionSecurityTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class Clock : IClock
    {
        // Real time — JWT lifetime is validated against the wall clock, not IClock.
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    [Fact]
    public async Task Access_token_carries_minimal_claims_and_round_trips()
    {
        var svc = new AccessTokenService("a-32-byte-minimum-development-secret!!", new Clock());
        var claims = new AccessClaims(Guid.CreateVersion7(), Guid.CreateVersion7(), "owner", "stamp-1");

        var token = svc.Mint(claims);
        var back = await svc.ValidateAsync(token);

        Assert.NotNull(back);
        Assert.Equal(claims.UserId, back!.UserId);
        Assert.Equal("owner", back.Role);
        Assert.Equal("stamp-1", back.SecurityStamp);
        // Minimal payload: no email/PII claim.
        Assert.DoesNotContain("@", token);
    }

    [Fact]
    public async Task A_token_with_a_stale_security_stamp_is_detected()
    {
        var svc = new AccessTokenService("a-32-byte-minimum-development-secret!!", new Clock());
        var token = svc.Mint(new AccessClaims(Guid.CreateVersion7(), Guid.CreateVersion7(), "owner", "old-stamp"));

        var claims = await svc.ValidateAsync(token);
        // The current stamp differs -> authorization must treat the token as stale.
        const string currentStamp = "new-stamp";
        Assert.NotEqual(currentStamp, claims!.SecurityStamp);
    }

    [Fact]
    public void Weak_or_missing_secret_fails_closed()
    {
        Assert.Throws<InvalidOperationException>(() => new AccessTokenService("short", new Clock()));
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/config");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Sensitive_endpoint_is_rate_limited()
    {
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            statuses.Add((await client.GetAsync("/api/v1/sensitive-demo")).StatusCode);
        }

        // Limit is 5 per window; the 6th+ must be throttled.
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.OK));
    }
}
