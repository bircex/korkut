using Korkut.Knowledge.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Korkut.Knowledge.Tests;

/// <summary>
/// The Knowledge (RAG) services as the app host composes them, over a real pgvector PostgreSQL
/// with RLS. A deterministic bag-of-words embedder makes ingest -> embed -> store -> cosine-search
/// fully reproducible, so relevance ranking, re-indexing, PII masking and tenant isolation are all
/// exercised end-to-end. Each scope acts as one request by a member of one office.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class KnowledgeServiceTests(KnowledgeFixture fixture)
{
    private const string Kira = "Kira sözleşmesi feshi konusunda kiracıya ihtarname gönderilir ve tahliye süreci başlar.";
    private const string Is = "İş hukuku kapsamında kıdem tazminatı ve ihbar tazminatı hesaplaması yapılır.";
    private const string Miras = "Miras hukukunda saklı pay oranları ve tenkis davası kanunla düzenlenir.";

    private async Task<int> IngestAsync(Guid office, Guid documentId, string text)
    {
        await using var scope = fixture.ScopeFor(office);
        return await scope.ServiceProvider.GetRequiredService<IIngestionService>().IngestAsync(documentId, text);
    }

    private async Task<Guid> IngestNewAsync(Guid office, string text)
    {
        var documentId = Guid.CreateVersion7();
        Assert.True(await IngestAsync(office, documentId, text) >= 1);
        return documentId;
    }

    private async Task<IReadOnlyList<SearchHit>> SearchAsync(Guid office, string query)
    {
        await using var scope = fixture.ScopeFor(office);
        return await scope.ServiceProvider.GetRequiredService<IRetrievalService>().SearchAsync(query, RetrievalService.DefaultTopK);
    }

    private async Task<List<string>> StoredTextsAsync(Guid documentId)
    {
        await using var db = fixture.KnowledgeOwnerContext();
        return await db.Chunks.AsNoTracking().Where(c => c.DocumentId == documentId).Select(c => c.Text).ToListAsync();
    }

    [Fact]
    public async Task Ingest_then_search_returns_the_most_relevant_document()
    {
        var office = await fixture.SeedOfficeAsync();
        var kiraDoc = await IngestNewAsync(office, Kira);
        await IngestNewAsync(office, Is);
        await IngestNewAsync(office, Miras);

        var hits = await SearchAsync(office, "kira feshi ihtarname tahliye");

        Assert.NotEmpty(hits);
        // The kira document is the closest match; its chunk ranks first.
        Assert.Equal(kiraDoc, hits[0].DocumentId);
        Assert.Contains("ihtarname", hits[0].Text);
        // Cosine similarity is reported as a score in [-1, 1].
        Assert.True(hits[0].Score > 0);
    }

    [Fact]
    public async Task Reingesting_a_document_replaces_its_chunks()
    {
        var office = await fixture.SeedOfficeAsync();
        var documentId = Guid.CreateVersion7();
        await IngestAsync(office, documentId, Kira);
        await IngestAsync(office, documentId, Miras);

        // Only the latest ingestion's chunks survive (no stale kira chunks linger).
        var texts = await StoredTextsAsync(documentId);
        Assert.NotEmpty(texts);
        Assert.All(texts, t => Assert.DoesNotContain("Kira", t));
        Assert.Contains(texts, t => t.Contains("Miras"));
    }

    [Fact]
    public async Task Ingesting_empty_text_clears_the_document_from_the_index()
    {
        var office = await fixture.SeedOfficeAsync();
        var documentId = Guid.CreateVersion7();
        await IngestAsync(office, documentId, Kira);

        Assert.Equal(0, await IngestAsync(office, documentId, ""));
        Assert.Empty(await StoredTextsAsync(documentId));
    }

    [Fact]
    public async Task Ingested_text_is_pii_masked_before_it_reaches_the_index()
    {
        var office = await fixture.SeedOfficeAsync();

        // 10000000146 is a checksum-valid test TCKN.
        var documentId = await IngestNewAsync(office, "Müvekkil ahmet@ornek.com, TCKN 10000000146 ile başvurdu.");

        // The vector index must never hold raw PII: the stored chunk text is masked.
        var stored = string.Join(" ", await StoredTextsAsync(documentId));
        Assert.NotEqual("", stored);
        Assert.DoesNotContain("ahmet@ornek.com", stored);
        Assert.DoesNotContain("10000000146", stored);
        Assert.Contains("[[PII_", stored);
    }

    [Fact]
    public async Task One_office_never_searches_another_offices_index()
    {
        var officeA = await fixture.SeedOfficeAsync();
        var officeB = await fixture.SeedOfficeAsync();

        await IngestNewAsync(officeA, Kira);

        Assert.Empty(await SearchAsync(officeB, "kira feshi ihtarname"));
    }

    [Fact]
    public async Task Chat_grounding_reads_the_same_index()
    {
        var office = await fixture.SeedOfficeAsync();
        var kiraDoc = await IngestNewAsync(office, Kira);

        await using var scope = fixture.ScopeFor(office);
        var hits = await scope.ServiceProvider.GetRequiredService<IKnowledgeSearchService>()
            .SearchAsync("kira feshi ihtarname", 3);

        Assert.Equal(kiraDoc, Assert.Single(hits).DocumentId);
    }
}
