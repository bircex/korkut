using System.Net;
using System.Net.Http.Json;
using Korkut.TestSupport;

namespace Korkut.LawClients.Integration.Tests;

/// <summary>
/// Routing + auth + DI wiring for /api/legal/* through the real API host: proves LawClientsModule
/// is actually reachable (ControllerSetup application part, DomainSetup registration,
/// documents.read policy) without calling any real public legal database — every case here fails
/// validation before the source client would make an outbound HTTP call.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class LegalControllerTests(KorkutApiFactory factory)
{
    [Fact]
    public async Task Search_without_authentication_is_rejected()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/legal/search?source=yargitay&query=tazminat");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Search_with_an_unsupported_source_is_a_bad_request()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.GetAsync("/api/legal/search?source=bilinmeyen&query=tazminat");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Contains("bilinmeyen", body!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_without_a_query_is_a_bad_request()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.GetAsync("/api/legal/search?source=yargitay");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_document_without_source_or_id_is_a_bad_request()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.GetAsync("/api/legal/documents");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_document_from_an_unsupported_source_is_not_found()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var response = await client.GetAsync("/api/legal/documents?source=bilinmeyen&id=1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record ErrorBody(string Detail);
}
