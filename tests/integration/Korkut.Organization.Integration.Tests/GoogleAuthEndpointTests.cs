using System.Net;
using System.Web;
using Korkut.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Integration.Tests;

/// <summary>
/// Google OIDC sign-in over the real API + real PostgreSQL (endpoint migration, slice
/// 7). The Google HTTP exchanger + JWKS validator are replaced with deterministic
/// fakes; the flow (state, nonce, cross-tenant resolution, session, redirect) is real.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class GoogleAuthEndpointTests(KorkutApiFactory factory)
{
    private static (string State, string Nonce) StartChallenge(string location)
    {
        var q = HttpUtility.ParseQueryString(new Uri(location).Query);
        return (q["state"]!, q["nonce"]!);
    }

    [Fact]
    public async Task Start_redirects_to_google_consent()
    {
        var client = factory.NoRedirectCookieClient();
        var resp = await client.GetAsync("/api/auth/google/start");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode); // 302
        var location = resp.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", location, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Callback_verifies_the_id_token_and_establishes_a_session()
    {
        // A user exists by email (no Google sub yet) — the callback links the sub.
        var (office, userId) = await factory.SeedOfficeAsync();
        string email;
        await using (var db = factory.OwnerContext())
        {
            email = await db.Users.Where(x => x.Id == userId).Select(x => x.Email).SingleAsync();
        }

        var client = factory.NoRedirectCookieClient();

        // 1) start → capture the server's state + nonce.
        var start = await client.GetAsync("/api/auth/google/start");
        var (state, nonce) = StartChallenge(start.Headers.Location!.ToString());

        // 2) callback with a (fake) id token carrying that nonce → session + redirect to "/".
        var code = KorkutApiFactory.FakeIdToken("google-sub-1", email, nonce);
        var cb = await client.GetAsync($"/api/auth/google/callback?state={state}&code={Uri.EscapeDataString(code)}");
        Assert.Equal(HttpStatusCode.SeeOther, cb.StatusCode); // 303
        Assert.Equal("/", cb.Headers.Location!.ToString());
        Assert.Contains(cb.Headers.GetValues("Set-Cookie"), c => c.StartsWith("korkut_at=", StringComparison.Ordinal));

        // The session cookie authenticates /api/me.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task A_replayed_state_is_rejected()
    {
        var client = factory.NoRedirectCookieClient();
        var start = await client.GetAsync("/api/auth/google/start");
        var (state, nonce) = StartChallenge(start.Headers.Location!.ToString());
        var code = KorkutApiFactory.FakeIdToken("sub", "someone@example.com", nonce);

        // First consume succeeds or fails on resolution; either way the challenge is now used.
        await client.GetAsync($"/api/auth/google/callback?state={state}&code={Uri.EscapeDataString(code)}");
        // Replaying the same state finds no challenge → redirect to the error page.
        var replay = await client.GetAsync($"/api/auth/google/callback?state={state}&code={Uri.EscapeDataString(code)}");
        Assert.Equal(HttpStatusCode.SeeOther, replay.StatusCode);
        Assert.Contains("error=verify", replay.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_google_account_that_is_not_a_member_is_refused()
    {
        var client = factory.NoRedirectCookieClient();
        var start = await client.GetAsync("/api/auth/google/start");
        var (state, nonce) = StartChallenge(start.Headers.Location!.ToString());
        // A verified e-mail that belongs to no member: members join by invitation, so no session
        // and no way to create an office from here.
        var code = KorkutApiFactory.FakeIdToken("sub-x", "stranger@nowhere.example", nonce);
        var cb = await client.GetAsync($"/api/auth/google/callback?state={state}&code={Uri.EscapeDataString(code)}");
        Assert.Equal(HttpStatusCode.SeeOther, cb.StatusCode);
        Assert.Equal("/giris?error=no_account", cb.Headers.Location!.ToString());
        Assert.False(cb.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith("korkut_at=", StringComparison.Ordinal)));
    }
}
