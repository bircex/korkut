using Korkut.TestSupport;
using Korkut.Agent.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Ledger.Tests;

/// <summary>
/// contentMd/title/summary are LLM-generated and can be derived from ingested document text (a
/// misread PDF/OCR page) — same NUL-byte exposure as chat message persistence
/// (SessionRepository.AppendMessageAsync), fixed here so SaveChangesAsync against real
/// Postgres never fails with 22021 (invalid byte sequence for encoding UTF8: 0x00).
/// </summary>
public class DraftDocumentRepositoryTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _office;

    public async Task InitializeAsync()
    {
        await pg.ResetAgentAsync();
        _office = Guid.CreateVersion7();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetOrCreateForSessionAsync_StripsNulBytesFromTitle()
    {
        await using var db = pg.NewAgentContext();
        var repo = new DraftDocumentRepository(db);

        var doc = await repo.GetOrCreateForSessionAsync(_office, Guid.CreateVersion7(), "Dilek\0çe", DateTimeOffset.UtcNow);

        Assert.DoesNotContain('\0', doc.Title);
        Assert.Equal("Dilekçe", doc.Title);
    }

    [Fact]
    public async Task StagePendingAsync_StripsNulBytesFromContentAndSummary_WithoutThrowing()
    {
        await using var db = pg.NewAgentContext();
        var repo = new DraftDocumentRepository(db);
        var doc = await repo.GetOrCreateForSessionAsync(_office, Guid.CreateVersion7(), "Belge", DateTimeOffset.UtcNow);

        var pendingId = await repo.StagePendingAsync(_office, doc.Id, "madde 1\0madde 2", "ozet\0metni", DateTimeOffset.UtcNow);

        Assert.NotNull(pendingId);
        var reloaded = await db.GeneratedDocuments.AsNoTracking().SingleAsync(x => x.Id == doc.Id);
        Assert.Equal("madde 1madde 2", reloaded.PendingContentMd);
        Assert.Equal("ozetmetni", reloaded.PendingSummary);
    }

    [Fact]
    public async Task SaveAsync_StripsNulBytesFromTitleAndContent_AndPersistsACleanVersion()
    {
        await using var db = pg.NewAgentContext();
        var repo = new DraftDocumentRepository(db);
        var doc = await repo.GetOrCreateForSessionAsync(_office, Guid.CreateVersion7(), "Belge", DateTimeOffset.UtcNow);

        var saved = await repo.SaveAsync(_office, doc.Id, "Yeni Ba\0slik", "govde\0metin", "avukat", DateTimeOffset.UtcNow);

        Assert.True(saved);
        var reloaded = await db.GeneratedDocuments.AsNoTracking().SingleAsync(x => x.Id == doc.Id);
        Assert.DoesNotContain('\0', reloaded.Title);
        Assert.DoesNotContain('\0', reloaded.ContentMd);
        var version = await db.DocumentVersions.AsNoTracking().SingleAsync(x => x.DocumentId == doc.Id);
        Assert.DoesNotContain('\0', version.ContentMd);
    }
}
