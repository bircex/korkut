using Korkut.TestSupport;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Organization.Tests;

/// <summary>
/// Self-serve onboarding over the real API + real PostgreSQL (endpoint migration, slice
/// 17): a verified Google email with no account → signup ticket → provision a DISABLED
/// office pending admin approval. Drives the real Google flow (deterministic fakes).
/// </summary>
[Collection(IntegrationSuite.Name)]
public class OnboardingEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static (string State, string Nonce) StartChallenge(string location)
    {
        var q = HttpUtility.ParseQueryString(new Uri(location).Query);
        return (q["state"]!, q["nonce"]!);
    }

    /// <summary>Run a Google sign-in for an account-less email; returns a cookie client holding the signup ticket.</summary>
    private async Task<HttpClient> SignupTicketClientAsync(string email, string sub)
    {
        var client = factory.NoRedirectCookieClient();
        var start = await client.GetAsync("/api/auth/google/start");
        var (state, nonce) = StartChallenge(start.Headers.Location!.ToString());
        var code = KorkutApiFactory.FakeIdToken(sub, email, nonce);
        var cb = await client.GetAsync($"/api/auth/google/callback?state={state}&code={Uri.EscapeDataString(code)}");
        Assert.Equal("/kayit", cb.Headers.Location!.ToString());
        return client;
    }

    [Fact]
    public async Task Signup_pending_reports_the_verified_email()
    {
        var client = await SignupTicketClientAsync("newlawyer@example.com", "gsub-onboard-1");
        var pending = await client.GetFromJsonAsync<JsonElement>("/api/signup/pending", Json);
        Assert.Equal("newlawyer@example.com", pending.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Demo_request_provisions_a_disabled_office_and_owner()
    {
        var email = "founder@example.com";
        var client = await SignupTicketClientAsync(email, "gsub-onboard-2");

        var res = await client.PostAsJsonAsync("/api/demo-request", new { officeName = "Yeni Hukuk Bürosu", fullName = "Av. Kurucu" }, Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("pending").GetBoolean());

        // A disabled office + owner user (linked to the Google sub) now exist.
        await using var db = factory.OwnerContext();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.Equal("owner", user.Role);
        Assert.Equal("gsub-onboard-2", user.GoogleSub);
        var office = await db.Offices.AsNoTracking().SingleAsync(o => o.Id == user.OfficeId);
        Assert.True(office.Disabled);
        Assert.Equal("Yeni Hukuk Bürosu", office.Name);

        // The ticket is consumed — a second signup/pending is now unauthorized.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/signup/pending")).StatusCode);
    }

    [Fact]
    public async Task Demo_request_without_a_ticket_is_unauthorized()
    {
        var anon = factory.CreateClient();
        var res = await anon.PostAsJsonAsync("/api/demo-request", new { officeName = "X" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Signup_pending_without_a_ticket_is_unauthorized()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/signup/pending")).StatusCode);
    }

    [Fact]
    public async Task An_already_registered_email_cannot_reprovision()
    {
        // Seed an existing account, then run signup for the same email.
        var (_, userId) = await factory.SeedOfficeAsync();
        string email;
        await using (var db = factory.OwnerContext())
        {
            email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).SingleAsync();
        }

        // The Google callback would LINK an existing single account rather than offer signup,
        // so drive demo-request directly against a ticket for that email via a fresh signup of
        // a *different* email, then confirm a duplicate email is refused by provisioning.
        var client = await SignupTicketClientAsync("brandnew@example.com", "gsub-onboard-3");
        // First provision succeeds.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/demo-request", new { officeName = "İlk" }, Json)).StatusCode);

        // A second signup ticket for the SAME new email now collides → conflict.
        var client2 = await SignupTicketClientAsync("brandnew@example.com", "gsub-onboard-3b");
        var res = await client2.PostAsJsonAsync("/api/demo-request", new { officeName = "İkinci" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        _ = email;
    }
}
