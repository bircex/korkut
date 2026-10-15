using System.Net;
using System.Net.Http.Json;
using Korkut.TestSupport;

namespace Korkut.Organization.Integration.Tests;

/// <summary>
/// Password-reset endpoints over the real API + real PostgreSQL (endpoint migration,
/// slice 6). The reset token is resolved cross-tenant via the BYPASSRLS SECURITY
/// DEFINER function; requests are enumeration-safe.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class PasswordResetEndpointTests(KorkutApiFactory factory)
{
    [Fact]
    public async Task Reset_request_is_always_200_for_known_and_unknown_emails()
    {
        var (_, _, email) = await factory.SeedUserWithPasswordAsync("eski-parola-123");
        var client = factory.CookieClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/reset-request", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/reset-request", new { email = "nobody@example.com" })).StatusCode);
    }

    [Fact]
    public async Task A_valid_token_resets_the_password_and_lets_the_user_log_in()
    {
        var (office, user, email) = await factory.SeedUserWithPasswordAsync("eski-parola-123");
        var raw = await factory.SeedResetTokenAsync(office, user);
        var client = factory.CookieClient();

        var reset = await client.PostAsJsonAsync("/api/auth/password/reset", new { token = raw, password = "yeni-parola-456" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // The new password now works; the old one does not.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "yeni-parola-456" })).StatusCode);
        var oldPw = await factory.CookieClient().PostAsJsonAsync("/api/auth/login", new { email, password = "eski-parola-123" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPw.StatusCode);
    }

    [Fact]
    public async Task Reset_request_issues_a_token_for_an_account_without_a_password_yet()
    {
        // A member the owner added has no password yet; a reset request must still issue a token
        // (and send the link), which is how they set their first one.
        var (_, user, email) = await factory.SeedPasswordlessUserAsync();
        var client = factory.CookieClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/reset-request", new { email })).StatusCode);
        Assert.Equal(1, await factory.CountActiveResetTokensAsync(user));
    }

    [Fact]
    public async Task Reset_request_issues_no_token_for_an_unknown_email()
    {
        // Guards the enumeration-safe no-op: unknown e-mail is still 200 but creates nothing.
        var (_, user, _) = await factory.SeedPasswordlessUserAsync();
        var client = factory.CookieClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/reset-request", new { email = "nobody@example.com" })).StatusCode);
        Assert.Equal(0, await factory.CountActiveResetTokensAsync(user));
    }

    [Fact]
    public async Task An_invalid_token_is_400()
    {
        var client = factory.CookieClient();
        var reset = await client.PostAsJsonAsync("/api/auth/password/reset", new { token = "not-a-real-token", password = "yeni-parola-456" });
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
    }

    [Fact]
    public async Task A_used_token_cannot_be_replayed()
    {
        var (office, user, _) = await factory.SeedUserWithPasswordAsync("eski-parola-123");
        var raw = await factory.SeedResetTokenAsync(office, user);
        var client = factory.CookieClient();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password/reset", new { token = raw, password = "yeni-parola-456" })).StatusCode);
        // Second use of the same token is rejected (single-use).
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/reset", new { token = raw, password = "baska-parola-789" })).StatusCode);
    }
}
