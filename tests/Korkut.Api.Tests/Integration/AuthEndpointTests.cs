using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;
using Korkut.Application.Security;

namespace Korkut.Api.Tests;

/// <summary>
/// Cookie-based auth/session over the real API + real PostgreSQL (endpoint migration,
/// slice 2). Proves the login → session-cookie → /me → refresh → logout loop, the
/// TOTP gate, and that tokens live only in httpOnly cookies.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class AuthEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_without_2fa_sets_httponly_cookies_and_me_returns_the_user()
    {
        var (_, _, email) = await factory.SeedUserWithPasswordAsync("dogru-parola-123");
        var client = factory.CookieClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "dogru-parola-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("totp_required").GetBoolean());

        // httpOnly session cookie set (not readable by JS).
        var setCookies = login.Headers.GetValues("Set-Cookie").ToList();
        Assert.Contains(setCookies, c => c.StartsWith("korkut_at=", StringComparison.Ordinal) && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookies, c => c.StartsWith("korkut_rt=", StringComparison.Ordinal));

        // The cookie authenticates /api/me.
        var me = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, meBody.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Wrong_password_is_401_and_sets_no_session()
    {
        var (_, _, email) = await factory.SeedUserWithPasswordAsync("dogru-parola-123");
        var client = factory.CookieClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "yanlis" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task A_2fa_user_gets_totp_required_and_no_full_session_yet()
    {
        var (_, _, email) = await factory.SeedUserWithPasswordAsync("parola-123", twoFactor: true);
        var client = factory.CookieClient();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "parola-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totp_required").GetBoolean());

        // No full session yet → /api/me is still unauthorized (only a pending cookie was set).
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_session_and_logout_clears_it()
    {
        var (_, _, email) = await factory.SeedUserWithPasswordAsync("parola-123");
        var client = factory.CookieClient();
        await client.PostAsJsonAsync("/api/auth/login", new { email, password = "parola-123" });

        // Refresh issues a new session and stays authenticated.
        var refresh = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);

        // Logout clears the session → /api/me unauthorized.
        var logout = await client.PostAsync("/api/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_email_is_a_generic_401()
    {
        var client = factory.CookieClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
