using System.Net;
using System.Net.Http.Headers;
using Korkut.Security;
using Korkut.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Korkut.Api.Tests;

public class AuthorizationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private readonly AuthorizationService _authz = new();

    // --- deny-by-default permission matrix (unit) ---

    [Theory]
    [InlineData(Roles.OfficeOwner, Permissions.MattersWrite, true)]
    [InlineData(Roles.OfficeOwner, Permissions.MembersManage, true)]
    [InlineData(Roles.OfficeOwner, Permissions.MattersDelete, true)]
    [InlineData(Roles.Lawyer, Permissions.MattersWrite, true)]
    [InlineData(Roles.Lawyer, Permissions.ClientsRead, true)]
    [InlineData(Roles.Lawyer, Permissions.MattersDelete, false)]   // lawyer can't delete
    [InlineData(Roles.Lawyer, Permissions.MembersManage, false)]   // lawyer can't manage members
    [InlineData(Roles.Lawyer, Permissions.OfficeSettings, false)]
    [InlineData("unknown-role", Permissions.MattersRead, false)]   // deny-by-default
    public void Permission_matrix_is_deny_by_default(string role, string permission, bool expected)
        => Assert.Equal(expected, _authz.HasPermission(role, permission));

    [Fact]
    public void Custom_permissions_augment_but_only_known_ones_count()
    {
        var perms = _authz.ResolvePermissions(Roles.Lawyer, [Permissions.BillingRead, "made.up.permission"]);
        Assert.Contains(Permissions.BillingRead, perms); // granted via custom role
        Assert.DoesNotContain("made.up.permission", perms); // unknown -> ignored
        Assert.DoesNotContain(Permissions.MembersManage, perms); // not granted
    }

    // --- policy enforcement over HTTP (integration) ---

    private static string Token(string role)
        => new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, new Clock())
            .Mint(new AccessClaims(Guid.CreateVersion7(), Guid.CreateVersion7(), role, "s1"));

    private HttpClient ClientWith(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    [Fact]
    public async Task Owner_can_call_a_matters_write_endpoint()
    {
        var resp = await ClientWith(Token(Roles.OfficeOwner)).PostAsync("/api/v1/matters-demo", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Lawyer_can_also_write_matters()
    {
        var resp = await ClientWith(Token(Roles.Lawyer)).PostAsync("/api/v1/matters-demo", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_denied()
    {
        var resp = await ClientWith(null).PostAsync("/api/v1/matters-demo", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Unknown_role_is_forbidden()
    {
        // Authenticated but with no permissions -> 403 (deny-by-default), not 401.
        var resp = await ClientWith(Token("intern-with-no-perms")).PostAsync("/api/v1/matters-demo", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
