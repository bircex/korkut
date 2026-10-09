using System.Net;
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
    public async Task An_unknown_api_path_is_a_404_not_the_app_shell()
    {
        var response = await factory.CreateClient().GetAsync("/api/no-such-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    // "/api/x/{id:guid}" and "api/x/{docId}" are the same route to the router.
    private static string Normalize(string raw)
        => string.Join('/', raw.Trim('/').Split('/').Select(s => s.StartsWith('{') ? "{}" : s.ToLowerInvariant()));
}
