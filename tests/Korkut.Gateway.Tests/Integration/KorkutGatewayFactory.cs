using System.Net.Http.Headers;
using Korkut.Application.Security;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Korkut.Gateway.Tests.Integration;

/// <summary>
/// Boots the real gateway with two lightweight STUB upstreams (one for the API cluster, one for
/// the Agent cluster) on random localhost ports. Each stub echoes which upstream it is, its path
/// and method — so a test can assert the gateway routed a request to the correct host. This
/// verifies routing + the edge auth gate end-to-end without standing up the real Api/Agent hosts.
/// </summary>
public sealed class KorkutGatewayFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private WebApplication _apiStub = null!;
    private WebApplication _agentStub = null!;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public async Task InitializeAsync()
    {
        _apiStub = BuildStub("api");
        _agentStub = BuildStub("agent");
        await _apiStub.StartAsync();
        await _agentStub.StartAsync();

        Environment.SetEnvironmentVariable("KORKUT_API_URL", _apiStub.Urls.First());
        Environment.SetEnvironmentVariable("KORKUT_AGENT_URL", _agentStub.Urls.First());
        _ = Server; // Trigger gateway construction now that the upstream URLs are known.
    }

    public new async Task DisposeAsync()
    {
        await _apiStub.DisposeAsync();
        await _agentStub.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KORKUT_API_URL", _apiStub.Urls.First());
        builder.UseSetting("KORKUT_AGENT_URL", _agentStub.Urls.First());
        builder.UseEnvironment("Testing");
    }

    // A stub upstream that answers every path/method with which host it is + the forwarded header.
    private static WebApplication BuildStub(string upstream)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Map("/{**catch-all}", (HttpContext ctx) => Results.Ok(new
        {
            upstream,
            path = ctx.Request.Path.Value,
            method = ctx.Request.Method,
            authorized = ctx.Request.Headers.ContainsKey("Authorization"),
        }));
        return app;
    }

    /// <summary>An HttpClient carrying a valid access token (any office/user — the edge only checks validity).</summary>
    public HttpClient AuthedClient()
    {
        var token = new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, new Clock())
            .Mint(new AccessClaims(Guid.CreateVersion7(), Guid.CreateVersion7(), Roles.OfficeOwner, "s1"));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
