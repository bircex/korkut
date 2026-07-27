using System.Net.Http.Headers;
using System.Text;
using Korkut.Application.Documents;
using Korkut.Application.Documents.OCR;
using Korkut.Infrastructure.Security;
using Korkut.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Korkut.Ocr.Tests.Integration;

/// <summary>Shared token minting for the OCR test hosts.</summary>
public abstract class OcrFactoryBase : WebApplicationFactory<Program>
{
    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    /// <summary>An HttpClient carrying a valid access token (owner → has matters.read).</summary>
    public HttpClient AuthedClient()
    {
        var token = new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, new Clock())
            .Mint(new AccessClaims(Guid.CreateVersion7(), Guid.CreateVersion7(), Roles.OfficeOwner, "s1"));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

/// <summary>Boots the OCR host with a deterministic fake engine (a real OCR backend stand-in).</summary>
public sealed class KorkutOcrFactory : OcrFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOcrEngine>();
            services.AddSingleton<IOcrEngine, FakeOcrEngine>();
        });
    }

    /// <summary>A stand-in engine: echoes the uploaded bytes as recovered "text" so the path is verifiable.</summary>
    private sealed class FakeOcrEngine : IOcrEngine
    {
        public bool IsAvailable => true;

        public async Task<OcrServiceResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
        {
            using var reader = new StreamReader(content, Encoding.UTF8);
            var text = await reader.ReadToEndAsync(ct);
            return new OcrServiceResult($"OCR: {text}", 1);
        }
    }
}

/// <summary>Boots the OCR host with the in-code scaffold engine (unavailable → 503).</summary>
public sealed class DisabledOcrFactory : OcrFactoryBase;
