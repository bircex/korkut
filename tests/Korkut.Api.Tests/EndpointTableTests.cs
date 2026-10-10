using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Api.Tests;

/// <summary>
/// The one host serves the operational API, the assistant and the app itself. Nothing routes by
/// path in front of it any more, so two controllers claiming the same method and route would only
/// show up as an ambiguous-match 500 on the first request; this catches it at build time instead.
/// </summary>
public class EndpointTableTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void No_two_endpoints_claim_the_same_method_and_route()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is not null)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => (Method: method, Route: Normalize(e.RoutePattern.RawText!), Name: e.DisplayName)))
            .ToList();

        var clashes = endpoints
            .GroupBy(e => (e.Method, e.Route))
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.Method} {g.Key.Route}: {string.Join(", ", g.Select(e => e.Name))}")
            .ToList();

        Assert.Empty(clashes);
    }

    [Fact]
    public void The_assistant_is_served_by_this_host()
    {
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => Normalize(e.RoutePattern.RawText ?? string.Empty))
            .ToHashSet();

        Assert.Contains("api/sessions", routes);
        Assert.Contains("api/draft-documents", routes);
        Assert.Contains("api/documents/{}/draft", routes);
    }

    [Fact]
    public async Task App_routes_get_the_shell_and_unknown_api_paths_do_not()
    {
        // The image ships the built app in wwwroot; stand one in so the fallback has a shell to serve.
        var webRoot = Directory.CreateTempSubdirectory("korkut-wwwroot-").FullName;
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), "<!doctype html><title>Korkut</title>");
        try
        {
            using var app = factory.WithWebHostBuilder(b => b.UseWebRoot(webRoot));
            var client = app.CreateClient();

            var shell = await client.GetAsync("/sohbet/12");
            Assert.Equal(HttpStatusCode.OK, shell.StatusCode);
            Assert.Equal("text/html", shell.Content.Headers.ContentType?.MediaType);
            Assert.Contains("script-src 'self'", shell.Headers.GetValues("Content-Security-Policy").Single());

            var missing = await client.GetAsync("/api/no-such-endpoint");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.NotEqual("text/html", missing.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    // "/api/x/{id:guid}" and "api/x/{docId}" are the same route to the router.
    private static string Normalize(string raw)
        => string.Join('/', raw.Trim('/').Split('/').Select(s => s.StartsWith('{') ? "{}" : s.ToLowerInvariant()));
}
