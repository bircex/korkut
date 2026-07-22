using System.Net;
using System.Net.Http.Headers;
using Korkut.Application.Security;
using Korkut.Infrastructure.Security;
using Korkut.Domain.Time;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Korkut.Api.Tests;

public class AdminApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private static string Secret => Environment.GetEnvironmentVariable("KORKUT_SECRET")!;

    // --- Allowlist (unit) ---

    [Fact]
    public void Allowlist_normalizes_and_rejects_outsiders()
    {
        Assert.True(AdminAllowlist.IsAllowed("  RecepKizilarslan@Gmail.com "));  // trims + lowercases
        Assert.False(AdminAllowlist.IsAllowed("attacker@evil.example"));
        Assert.False(AdminAllowlist.IsAllowed(""));
    }

    [Fact]
    public void Superadmin_env_var_is_never_consulted()
    {
        Environment.SetEnvironmentVariable("KORKUT_SUPERADMIN_EMAILS", "attacker@evil.example");
        try
        {
            Assert.False(AdminAllowlist.IsAllowed("attacker@evil.example"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("KORKUT_SUPERADMIN_EMAILS", null);
        }
    }

    // --- Sign-in gate (unit with fakes) ---

    private sealed class FakeFlow : IOidcFlowStore
    {
        private OidcChallenge? _c;
        public Task SaveAsync(OidcChallenge c, DateTimeOffset e, CancellationToken ct = default) { _c = c; return Task.CompletedTask; }
        public Task<OidcChallenge?> ConsumeAsync(string s, DateTimeOffset n, CancellationToken ct = default)
        { var c = _c; _c = null; return Task.FromResult(c); }
    }

    private sealed class FakeExch : IOidcTokenExchanger
    { public Task<string> ExchangeForIdTokenAsync(string c, string v, CancellationToken ct = default) => Task.FromResult("t"); }

    private sealed class FakeVal(GoogleIdentity id) : IGoogleIdTokenValidator
    { public Task<GoogleIdentity> ValidateAsync(string t, CancellationToken ct = default) => Task.FromResult(id); }

    private sealed class FakeEnroll(bool enrolled) : IAdminEnrollment
    { public Task<bool> IsEnrolledAsync(string e, CancellationToken ct) => Task.FromResult(enrolled); }

    private static async Task<AdminSignInResult> RunAsync(GoogleIdentity id, bool enrolled)
    {
        var flow = new FakeFlow();
        var challenge = OidcChallenge.Create() with { Nonce = "n1" };
        await flow.SaveAsync(challenge, DateTimeOffset.UtcNow.AddMinutes(5));
        var svc = new AdminSignInService(flow, new FakeExch(), new FakeVal(id), new FakeEnroll(enrolled));
        return await svc.CompleteAsync(challenge.State, "code");
    }

    [Fact]
    public async Task Non_allowlisted_verified_email_is_rejected()
    {
        var result = await RunAsync(new GoogleIdentity("s", "outsider@evil.example", true, "n1"), enrolled: true);
        Assert.Equal(AdminSignInStatus.Rejected, result.Status);
        Assert.Equal("not_allowlisted", result.Reason);
    }

    [Fact]
    public async Task Allowlisted_enrolled_admin_still_needs_totp_not_a_session()
    {
        var result = await RunAsync(new GoogleIdentity("s", "recepkizilarslan@gmail.com", true, "n1"), enrolled: true);
        Assert.Equal(AdminSignInStatus.TwoFactorRequired, result.Status); // never a session from the callback
    }

    [Fact]
    public async Task Allowlisted_unenrolled_admin_must_bootstrap()
    {
        var result = await RunAsync(new GoogleIdentity("s", "recepkizilarslan@gmail.com", true, "n1"), enrolled: false);
        Assert.Equal(AdminSignInStatus.BootstrapRequired, result.Status);
    }

    // --- Admin endpoint boundary (integration) ---

    [Fact]
    public async Task Admin_endpoint_accepts_an_admin_token()
    {
        var admin = new AdminAccessTokens(Secret, new Clock())
            .Mint(new AccessClaims(Guid.CreateVersion7(), Guid.Empty, "platform_admin", "s1"));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/ping")).StatusCode);
    }

    [Fact]
    public async Task Office_token_is_rejected_at_the_admin_endpoint()
    {
        // An office-audience token must not authenticate against the admin boundary.
        var office = new AccessTokenService(Secret, new Clock())
            .Mint(new AccessClaims(Guid.CreateVersion7(), Guid.CreateVersion7(), "owner", "s1"));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", office);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/ping")).StatusCode);
    }
}
