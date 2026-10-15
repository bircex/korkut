using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Api.Tests;

/// <summary>
/// Every endpoint says who may call it. One that says nothing would fall to the fallback policy
/// (signed in, no permission), which is rarely what an office endpoint should be; one marked
/// anonymous is open to the world. Both are pinned here, so adding either is a reviewed change.
/// </summary>
public class AccessDeclarationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>Open without a session: signing in, recovery, invites, the app shell, health, signed file links.</summary>
    private static readonly string[] Anonymous =
    [
        // The app shell and its client-side routes, and the 404 for unknown /api paths.
        "GET {*path:nonfile}",
        "HEAD {*path:nonfile}",
        "* api/{**rest}",
        // Liveness and readiness probes, and the public API description.
        "* health/live",
        "* health/ready",
        "GET openapi/{}",
        // Signing in and out, two-step verification, password recovery.
        "POST api/auth/login",
        "POST api/auth/2fa",
        "POST api/auth/refresh",
        "POST api/auth/password/reset-request",
        "POST api/auth/password/reset",
        "POST api/logout",
        // Joining through an invite link.
        "GET api/invites/peek",
        "POST api/invites/accept",
        "POST api/invites/accept-local",
        // Short-lived HMAC-signed download links (the signature is the authorization).
        "GET api/documents/files/{}",
        // The plumbing contract endpoints (no data).
        "GET api/v1/samples",
        "POST api/v1/echo",
        "GET api/v1/not-found-demo",
        "GET api/v1/sensitive-demo",
    ];

    /// <summary>Any signed-in member, whatever their role: their own account and the stateless calculators.</summary>
    private static readonly string[] SignedInOnly =
    [
        "GET api/me",
        "PATCH api/me",
        "POST api/me/password",
    ];

    private List<(string Key, IReadOnlyList<object> Metadata)> Endpoints()
        => factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(m => ($"{m} {Normalize(e.RoutePattern.RawText ?? string.Empty)}", (IReadOnlyList<object>)e.Metadata.ToList())))
            .ToList();

    [Fact]
    public void Every_endpoint_declares_who_may_call_it()
    {
        var undeclared = Endpoints()
            .Where(e => !e.Metadata.OfType<IAllowAnonymous>().Any() && !e.Metadata.OfType<IAuthorizeData>().Any())
            .Select(e => e.Key)
            .ToList();

        Assert.Empty(undeclared);
    }

    [Fact]
    public void Only_the_known_endpoints_are_anonymous()
    {
        var anonymous = Endpoints()
            .Where(e => e.Metadata.OfType<IAllowAnonymous>().Any())
            .Select(e => e.Key)
            .Distinct()
            .Order()
            .ToList();

        Assert.True(Anonymous.Order().SequenceEqual(anonymous), "Anonymous endpoints now: " + string.Join(" | ", anonymous));
    }

    [Fact]
    public void Every_other_endpoint_names_a_permission()
    {
        var withoutPermission = Endpoints()
            .Where(e => !e.Metadata.OfType<IAllowAnonymous>().Any())
            .Where(e => e.Metadata.OfType<IAuthorizeData>().All(a => string.IsNullOrEmpty(a.Policy)))
            .Select(e => e.Key)
            .Where(k => !SignedInOnly.Contains(k) && !k.Contains(" api/calc", StringComparison.Ordinal))
            .Distinct()
            .Order()
            .ToList();

        Assert.Empty(withoutPermission);
    }

    [Fact]
    public async Task Office_endpoints_refuse_a_caller_without_a_session()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/matters")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PatchAsync($"/api/matters/{Guid.NewGuid()}", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task The_app_and_its_files_load_without_a_session()
    {
        var webRoot = Directory.CreateTempSubdirectory("korkut-wwwroot-").FullName;
        Directory.CreateDirectory(Path.Combine(webRoot, "assets"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><title>Korkut</title>");
        await File.WriteAllTextAsync(Path.Combine(webRoot, "assets", "app-1a2b.js"), "console.log(1)");
        try
        {
            using var app = factory.WithWebHostBuilder(b => b.UseWebRoot(webRoot));
            var client = app.CreateClient();

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/giris")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/assets/app-1a2b.js")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/index.html")).StatusCode);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private static string Normalize(string raw)
        => string.Join('/', raw.Trim('/').Split('/').Select(s => s.StartsWith('{') && !s.StartsWith("{**", StringComparison.Ordinal) && !s.Contains(':') ? "{}" : s));
}
