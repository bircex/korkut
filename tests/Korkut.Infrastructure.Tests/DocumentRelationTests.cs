using Korkut.Infrastructure.Auditing;
using Korkut.Application.Errors;
using Korkut.Domain.Documents;
using Korkut.Domain.Offices;
using System.Text;
using Korkut.Application.Documents;
using Korkut.Infrastructure.Documents;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Korkut.Domain.Clients;
using Korkut.Domain.Matters;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Typed document relations, index outbox events, and their RLS/audit/idempotency
/// guarantees over real Postgres + MinIO (KDM-028).
/// </summary>
public class DocumentRelationTests(PostgresFixture pg, MinioFixture minio)
    : IClassFixture<PostgresFixture>, IClassFixture<MinioFixture>, IAsyncLifetime
{
    private Guid _office;
    private Guid _actor;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _office = Guid.CreateVersion7();
        _actor = Guid.CreateVersion7();
        await using var db = pg.NewContext();
        db.Offices.Add(new Office(_office, "O", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static DocumentRelationService Relations(KorkutDbContext db) => new(db, new AuditLog(db, new Clock()), new Clock());

    private async Task<Guid> SeedDocumentAsync(KorkutDbContext db)
    {
        var stored = await new DocumentStorageService(db, minio.StoreService, new Clock())
            .StoreAsync(_office, null, new MemoryStream("x"u8.ToArray()), "d.txt", "text/plain");
        await db.Documents.Where(x => x.Id == stored.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "clean"), default);
        return stored.Id;
    }

    private async Task<Guid> SeedClientAsync(KorkutDbContext db)
    {
        var client = new Client(_office, "c", "individual", null, null, DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client.Id;
    }

    private async Task<Guid> SeedMatterAsync(KorkutDbContext db)
    {
        var matter = new Matter(_office, "m", null, null, null, DateTimeOffset.UtcNow);
        db.Matters.Add(matter);
        await db.SaveChangesAsync();
        return matter.Id;
    }

    [Fact]
    public async Task A_document_can_be_linked_to_multiple_targets_and_the_link_is_audited()
    {
        await using var db = pg.NewContext();
        var doc = await SeedDocumentAsync(db);
        var client = await SeedClientAsync(db);
        var matter = await SeedMatterAsync(db);

        await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, client, isPrimary: true);
        await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Matter, matter, isPrimary: false);

        var links = await db.DocumentRelations.AsNoTracking().Where(x => x.DocumentId == doc).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.Single(links, x => x.IsPrimary); // exactly one primary

        var audited = await db.AuditEvents.AsNoTracking().CountAsync(x => x.Action == "document.relation.linked");
        Assert.Equal(2, audited);
    }

    [Fact]
    public async Task Re_linking_the_same_pair_is_idempotent_and_only_moves_the_primary_flag()
    {
        await using var db = pg.NewContext();
        var doc = await SeedDocumentAsync(db);
        var a = await SeedClientAsync(db);
        var b = await SeedClientAsync(db);

        await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, a, isPrimary: true);
        await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, b, isPrimary: true); // steals primary
        await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, a, isPrimary: false); // repeat A

        var links = await db.DocumentRelations.AsNoTracking().Where(x => x.DocumentId == doc).ToListAsync();
        Assert.Equal(2, links.Count); // no duplicate row for A
        Assert.Equal(b, Assert.Single(links, x => x.IsPrimary).TargetId); // primary moved to B
    }

    [Fact]
    public async Task Unlink_removes_the_relation_and_audits_it()
    {
        await using var db = pg.NewContext();
        var doc = await SeedDocumentAsync(db);
        var client = await SeedClientAsync(db);
        await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, client, isPrimary: false);

        await Relations(db).UnlinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, client);

        Assert.Equal(0, await db.DocumentRelations.CountAsync(x => x.DocumentId == doc));
        Assert.Equal(1, await db.AuditEvents.CountAsync(x => x.Action == "document.relation.unlinked"));
    }

    [Fact]
    public async Task Linking_a_target_from_another_office_is_refused()
    {
        await using var db = pg.NewContext();
        var doc = await SeedDocumentAsync(db);
        var strangerClient = Guid.CreateVersion7(); // never created in this office

        await Assert.ThrowsAsync<NotFoundException>(
            () => Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, strangerClient, isPrimary: false));
        Assert.Equal(0, await db.DocumentRelations.CountAsync(x => x.DocumentId == doc));
    }

    [Fact]
    public async Task Permission_inheritance_lists_documents_reachable_through_a_target()
    {
        await using var db = pg.NewContext();
        var d1 = await SeedDocumentAsync(db);
        var d2 = await SeedDocumentAsync(db);
        var matter = await SeedMatterAsync(db);
        await Relations(db).LinkAsync(_office, _actor, d1, DocumentRelationTarget.Matter, matter, isPrimary: true);
        await Relations(db).LinkAsync(_office, _actor, d2, DocumentRelationTarget.Matter, matter, isPrimary: true);

        var reachable = await Relations(db).DocumentsForTargetAsync(_office, DocumentRelationTarget.Matter, matter);
        Assert.Equal([d1, d2], reachable.OrderBy(x => x).ToArray().OrderBy(x => x));
        Assert.Contains(d1, reachable);
        Assert.Contains(d2, reachable);
    }

    [Fact]
    public async Task Relations_are_tenant_isolated_under_rls()
    {
        // Seed a relation as superuser, then read as the least-privileged app role
        // scoped to a DIFFERENT office — RLS must hide it.
        Guid doc;
        Guid client;
        await using (var db = pg.NewContext())
        {
            doc = await SeedDocumentAsync(db);
            client = await SeedClientAsync(db);
            await Relations(db).LinkAsync(_office, _actor, doc, DocumentRelationTarget.Client, client, isPrimary: false);
        }

        await using var app = pg.NewAppContext();
        await app.Database.OpenConnectionAsync();
        var other = Guid.CreateVersion7();
        await app.Database.ExecuteSqlRawAsync("SELECT set_config('app.current_office', {0}, false)", other.ToString());
        Assert.Equal(0, await app.DocumentRelations.CountAsync());

        await app.Database.ExecuteSqlRawAsync("SELECT set_config('app.current_office', {0}, false)", _office.ToString());
        Assert.Equal(1, await app.DocumentRelations.CountAsync());
    }

    [Fact]
    public async Task Extraction_enqueues_exactly_one_index_event_per_version_and_tombstone_a_deindex_event()
    {
        await using var db = pg.NewContext();
        var doc = await SeedDocumentAsync(db);
        var clock = new Clock();

        // Extract twice (retry): still one index event (idempotent projection).
        await new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), clock).ExtractAsync(_office, doc);
        await new ExtractionService(db, minio.StoreService, new PlainTextExtractorService(), clock).ExtractAsync(_office, doc);
        Assert.Equal(1, await db.Outbox.CountAsync(x => x.Type == DocumentIndexEventTypes.Index));

        // Tombstone -> a de-index event that preserves provenance.
        await new DocumentLifecycleService(db, minio.StoreService, clock).TombstoneAsync(_office, doc);
        var deindex = await db.Outbox.AsNoTracking().SingleAsync(x => x.Type == DocumentIndexEventTypes.Deindex);
        Assert.Contains(doc.ToString(), deindex.Payload, StringComparison.Ordinal);
        Assert.Contains(_office.ToString(), deindex.Payload, StringComparison.Ordinal);
    }
}
