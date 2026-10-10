using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Korkut.Contract.Documents;
using Korkut.Core;
using Korkut.Jobs.Abstractions;
using Korkut.Jobs.Services;
using Korkut.Knowledge;
using Korkut.Knowledge.Persistence;
using Korkut.Security;
using Korkut.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;

namespace Korkut.Documents.Integration.Tests;

/// <summary>
/// An uploaded document reaches the search index the assistant grounds its answers on, and
/// leaves it when deleted: upload (extraction queues the index job) → the job runs → chunks
/// exist → delete → the de-index job runs → chunks are gone. Real API, real Postgres with RLS,
/// a deterministic embedder in place of a model.
/// </summary>
[Collection(IntegrationSuite.Name)]
public class DocumentSearchIndexTests(KorkutApiFactory factory) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private WebApplicationFactory<Program> _app = null!;

    public async Task InitializeAsync()
    {
        // The shared host migrates only the operational schema; the index lives in its own.
        await using (var knowledge = KnowledgeOwnerContext())
        {
            await knowledge.Database.MigrateAsync();
        }

        _app = factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbedder>();
            services.AddSingleton<IEmbedder, FixedEmbedder>();
        }));
    }

    public Task DisposeAsync() => _app.DisposeAsync().AsTask();

    [Fact]
    public async Task An_uploaded_document_is_indexed_and_a_deleted_one_is_removed()
    {
        var (office, user) = await factory.SeedOfficeAsync();
        var client = ClientFor(office, user);

        var text = Encoding.UTF8.GetBytes("Kira sözleşmesinin feshi için kiracıya ihtarname gönderildi.");
        var up = await client.PostAsync("/api/documents", Upload(text, "ihtarname.txt", "text/plain"));
        Assert.Equal(HttpStatusCode.Created, up.StatusCode);
        var documentId = (await up.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        // Extraction ran in the upload and queued the index job.
        await using (var db = factory.OwnerContext())
        {
            Assert.True(await db.Outbox.AnyAsync(m => m.OfficeId == office && m.Type == DocumentIndexEventTypes.Index
                && m.Payload.Contains(documentId.ToString())));
        }

        await RunJobsAsync();
        var indexed = await ChunksAsync(documentId);
        Assert.NotEmpty(indexed);
        Assert.Contains(indexed, t => t.Contains("ihtarname"));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/documents/{documentId}")).StatusCode);
        await RunJobsAsync();
        Assert.Empty(await ChunksAsync(documentId));
    }

    private async Task RunJobsAsync()
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var s = scope.ServiceProvider;
        var dispatcher = new OutboxDispatcher(
            s.GetRequiredService<IOutboxStore>(), s.GetRequiredService<IInboxStore>(), s.GetRequiredService<IJobAudit>(),
            s.GetServices<IJobHandler>().ToDictionary(h => h.MessageType), s.GetRequiredService<ITenantContext>(),
            s.GetRequiredService<IRetryPolicy>(), s.GetRequiredService<IClock>());
        while (await dispatcher.DispatchOnceAsync("test", 50, TimeSpan.FromMinutes(1)) > 0)
        {
        }
    }

    private async Task<List<string>> ChunksAsync(Guid documentId)
    {
        await using var db = KnowledgeOwnerContext();
        return await db.Chunks.AsNoTracking().Where(c => c.DocumentId == documentId).Select(c => c.Text).ToListAsync();
    }

    private KnowledgeDbContext KnowledgeOwnerContext()
    {
        using var ops = factory.OwnerContext();
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(ops.Database.GetConnectionString(), npgsql =>
            {
                npgsql.MigrationsHistoryTable(KnowledgeSetup.MigrationsHistoryTable, KnowledgeSetup.KnowledgeSchema);
                npgsql.UseVector();
            })
            .Options;
        return new KnowledgeDbContext(options);
    }

    private HttpClient ClientFor(Guid office, Guid user)
    {
        var token = new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, SystemClock.Instance)
            .Mint(new AccessClaims(user, office, Roles.OfficeOwner, "s1"));
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string filename, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", filename);
        return form;
    }

    /// <summary>The same vector for every text: enough to store chunks; ranking is tested elsewhere.</summary>
    private sealed class FixedEmbedder : IEmbedder
    {
        public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult(new EmbeddingResult(
                texts.Select(_ => Enumerable.Repeat(0.125f, 8).ToArray()).ToList(), "fixed", "fixed:8", 8, 0));
    }
}
