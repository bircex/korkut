using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Knowledge.Tests.Integration;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Knowledge.Tests;

/// <summary>
/// The Knowledge (RAG) service over the real host + real pgvector PostgreSQL with RLS. A
/// deterministic bag-of-words embedder makes ingest → embed → store → cosine-search fully
/// reproducible, so relevance ranking, re-indexing, and tenant isolation are all exercised
/// end-to-end.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class KnowledgeEndpointTests(KorkutKnowledgeFactory factory)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private const string Kira = "Kira sözleşmesi feshi konusunda kiracıya ihtarname gönderilir ve tahliye süreci başlar.";
    private const string Is = "İş hukuku kapsamında kıdem tazminatı ve ihbar tazminatı hesaplaması yapılır.";
    private const string Miras = "Miras hukukunda saklı pay oranları ve tenkis davası kanunla düzenlenir.";

    private static async Task<Guid> IngestAsync(HttpClient client, string text)
    {
        var docId = Guid.CreateVersion7();
        var res = await client.PostAsJsonAsync("/ingest", new { document_id = docId, text }, Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("chunks").GetInt32() >= 1);
        return docId;
    }

    private static async Task<JsonElement> SearchAsync(HttpClient client, string query, int? topK = null)
    {
        var res = await client.PostAsJsonAsync("/search", new { query, top_k = topK }, Json);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task Ingest_then_search_returns_the_most_relevant_document()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var kiraDoc = await IngestAsync(client, Kira);
        await IngestAsync(client, Is);
        await IngestAsync(client, Miras);

        var result = await SearchAsync(client, "kira feshi ihtarname tahliye");
        var hits = result.GetProperty("hits").EnumerateArray().ToList();
        Assert.NotEmpty(hits);
        // The kira document is the closest match; its chunk ranks first.
        Assert.Equal(kiraDoc, hits[0].GetProperty("document_id").GetGuid());
        Assert.Contains("ihtarname", hits[0].GetProperty("text").GetString());
        // Cosine similarity is reported as a score in [-1, 1].
        Assert.True(hits[0].GetProperty("score").GetDouble() > 0);
    }

    [Fact]
    public async Task Reingesting_a_document_replaces_its_chunks()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var docId = Guid.CreateVersion7();
        await client.PostAsJsonAsync("/ingest", new { document_id = docId, text = Kira }, Json);
        await client.PostAsJsonAsync("/ingest", new { document_id = docId, text = Miras }, Json);

        // Only the latest ingestion's chunks survive (no stale kira chunks linger).
        await using var db = factory.KnowledgeOwnerContext();
        var texts = await db.Chunks.AsNoTracking().Where(c => c.DocumentId == docId).Select(c => c.Text).ToListAsync();
        Assert.NotEmpty(texts);
        Assert.All(texts, t => Assert.DoesNotContain("Kira", t));
        Assert.Contains(texts, t => t.Contains("Miras"));
    }

    [Fact]
    public async Task Ingesting_empty_text_clears_the_document_from_the_index()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = factory.ClientFor(office, user);

        var docId = Guid.CreateVersion7();
        await client.PostAsJsonAsync("/ingest", new { document_id = docId, text = Kira }, Json);
        var cleared = await client.PostAsJsonAsync("/ingest", new { document_id = docId, text = "" }, Json);
        var body = await cleared.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, body.GetProperty("chunks").GetInt32());

        await using var db = factory.KnowledgeOwnerContext();
        Assert.Equal(0, await db.Chunks.AsNoTracking().CountAsync(c => c.DocumentId == docId));
    }

    [Fact]
    public async Task One_office_never_searches_another_offices_index()
    {
        var (officeA, userA) = await factory.SeedOfficeAsync();
        var (officeB, userB) = await factory.SeedOfficeAsync();
        var clientA = factory.ClientFor(officeA, userA);
        var clientB = factory.ClientFor(officeB, userB);

        await IngestAsync(clientA, Kira);

        var result = await SearchAsync(clientB, "kira feshi ihtarname");
        Assert.Empty(result.GetProperty("hits").EnumerateArray());
    }

    [Fact]
    public async Task Anonymous_is_rejected()
    {
        var anon = factory.CreateClient();
        var search = await anon.PostAsJsonAsync("/search", new { query = "x" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, search.StatusCode);
        var ingest = await anon.PostAsJsonAsync("/ingest", new { document_id = Guid.NewGuid(), text = "x" }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, ingest.StatusCode);
    }
}
