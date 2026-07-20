using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Api.Tests.Integration;

namespace Korkut.Api.Tests;

/// <summary>
/// AI-drafted documents over the real API + real PostgreSQL with RLS (endpoint migration,
/// slice 18e). A per-session document, LLM-staged pending edits (fake LLM), accept/revert,
/// direct save + versioning, and export. Exercises the full draft → accept → export path.
/// </summary>
[Collection(Integration.IntegrationSuite.Name)]
public class DraftDocumentEndpointTests(KorkutApiFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private async Task<(HttpClient Client, Guid SessionId, Guid DocId)> NewDocAsync()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);
        var session = await (await client.PostAsync("/api/sessions", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var sid = session.GetProperty("id").GetGuid();
        var doc = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sid}/document", Json);
        return (client, sid, doc.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_document_creates_one_per_session()
    {
        var (client, sid, docId) = await NewDocAsync();
        Assert.NotEqual(Guid.Empty, docId);
        // Idempotent: a second GET returns the same document.
        var again = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sid}/document", Json);
        Assert.Equal(docId, again.GetProperty("id").GetGuid());
        Assert.Equal("", again.GetProperty("content_md").GetString());
        Assert.Equal(JsonValueKind.Null, again.GetProperty("pending").ValueKind);
    }

    [Fact]
    public async Task Draft_then_accept_applies_the_pending_edit_and_versions_it()
    {
        var (client, _, docId) = await NewDocAsync();

        // Draft — the fake LLM echoes the drafting prompt, which contains the instruction.
        var pending = await (await client.PostAsJsonAsync($"/api/documents/{docId}/draft",
            new { instruction = "Bir tahliye ihtarnamesi yaz", petitionType = (string?)null }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("tahliye ihtarnamesi", pending.GetProperty("content_md").GetString());
        Assert.Contains("tahliye ihtarnamesi", pending.GetProperty("summary").GetString());

        // Accept → content becomes the pending markdown, pending cleared, a version recorded.
        var accepted = await (await client.PostAsync($"/api/documents/{docId}/accept", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("tahliye ihtarnamesi", accepted.GetProperty("content_md").GetString());
        Assert.Equal(JsonValueKind.Null, accepted.GetProperty("pending").ValueKind);
        Assert.Equal(1, accepted.GetProperty("versions").GetArrayLength());
    }

    [Fact]
    public async Task Revert_discards_the_pending_edit()
    {
        var (client, _, docId) = await NewDocAsync();
        await client.PostAsJsonAsync($"/api/documents/{docId}/draft", new { instruction = "X yaz" }, Json);
        var reverted = await (await client.PostAsync($"/api/documents/{docId}/revert", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(JsonValueKind.Null, reverted.GetProperty("pending").ValueKind);
        Assert.Equal("", reverted.GetProperty("content_md").GetString());
    }

    [Fact]
    public async Task Save_sets_content_and_records_a_version_then_export_renders_it()
    {
        var (client, _, docId) = await NewDocAsync();

        var saved = await (await client.PutAsJsonAsync($"/api/documents/{docId}",
            new { title = "İhtarname", contentMd = "# İhtarname\n\nSayın ilgili…" }, Json))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("İhtarname", saved.GetProperty("title").GetString());
        Assert.Equal(1, saved.GetProperty("versions").GetArrayLength());

        // Export renders the saved content to a valid docx package.
        var res = await client.GetAsync($"/api/documents/{docId}/export?format=docx");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("word/document.xml"));
    }

    [Fact]
    public async Task One_office_never_sees_another_offices_document()
    {
        var (_, _, docId) = await NewDocAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientB = factory.ClientFor(officeB, userB);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostAsync($"/api/documents/{docId}/accept", null)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/documents/{Guid.NewGuid()}/export?format=docx")).StatusCode);
    }
}
