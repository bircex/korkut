using Korkut.TestSupport;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Korkut.Api.Tests.Integration;

namespace Korkut.Api.Tests;

/// <summary>
/// Secure-document endpoints over the real API + real PostgreSQL + real MinIO (endpoint
/// migration, slice 3): quarantine upload, download gated on clean, rejection of a
/// spoofed file, and tenant isolation.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class DocumentEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static MultipartFormDataContent Upload(byte[] bytes, string filename, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", filename);
        return form;
    }

    private static byte[] Pdf() => Encoding.ASCII.GetBytes("%PDF-1.7\nsözleşme metni\n%%EOF");

    [Fact]
    public async Task Clean_pdf_uploads_to_quarantine_lists_and_then_downloads_when_clean()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        // Upload — a clean PDF is promoted to clean by the pipeline.
        var up = await client.PostAsync("/api/documents", Upload(Pdf(), "sozlesme.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var doc = await up.Content.ReadFromJsonAsync<JsonElement>();
        var id = doc.GetProperty("id").GetGuid();
        Assert.Equal("clean", doc.GetProperty("status").GetString());

        // List shows it.
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/documents", Json);
        Assert.Contains(list!, d => d.GetProperty("id").GetGuid() == id);

        // Download → a presigned URL.
        var dl = await client.GetAsync($"/api/documents/{id}/download");
        Assert.Equal(HttpStatusCode.OK, dl.StatusCode);
        var url = (await dl.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString();
        Assert.Contains("X-Amz-", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_spoofed_executable_named_pdf_is_rejected()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        // MZ header (PE/exe) with a .pdf name → magic-byte mismatch → 422, never stored.
        var up = await client.PostAsync("/api/documents", Upload([0x4D, 0x5A, 0x90, 0x00], "document.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, up.StatusCode);
    }

    [Fact]
    public async Task Documents_are_tenant_isolated()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();

        var a = factory.ClientFor(officeA, userA);
        var up = await a.PostAsync("/api/documents", Upload(Pdf(), "a.pdf", "application/pdf"));
        var id = (await up.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var b = factory.ClientFor(officeB, userB);
        var bList = await b.GetFromJsonAsync<List<JsonElement>>("/api/documents", Json);
        Assert.DoesNotContain(bList!, d => d.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/documents/{id}/download")).StatusCode);
    }

    [Fact]
    public async Task Delete_tombstones_the_document()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var up = await client.PostAsync("/api/documents", Upload(Pdf(), "sil.pdf", "application/pdf"));
        var id = (await up.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{id}")).StatusCode);
        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/documents", Json);
        Assert.DoesNotContain(list!, d => d.GetProperty("id").GetGuid() == id);
    }
}
