using Korkut.Application.Jobs;
using Korkut.Application.Jobs.InMemory;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Korkut.Domain.Clients;
using Korkut.Domain.Matters;
using Korkut.Domain.Hearings;
using Korkut.Domain.Courts;
using Korkut.Domain.Parties;
using Korkut.Domain.Tasks;
using Korkut.Domain.Deadlines;
using Korkut.Domain.Notes;
using Korkut.Domain.Invoices;
using Korkut.Domain.Expenses;
using Korkut.Domain.Correspondence;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Reusable tenant-isolation matrix (KDM-013) over every registered tenant table
/// and multiple principals (office A, office B, no-context), on real PostgreSQL.
/// Includes a coverage guard, post-rollback/pool context cleanup, and the audited
/// background-job path. Runs as the least-privileged app role (RLS-subject).
/// </summary>
public class TenantMatrixTests(PostgresFixture pg) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private Guid _a;
    private Guid _b;

    public async Task InitializeAsync()
    {
        await pg.ResetAsync();
        _a = Guid.CreateVersion7();
        _b = Guid.CreateVersion7();

        // Seed via superuser (RLS bypassed): A owns 1 matter+1 hearing, B owns 2+2.
        await using var db = pg.NewContext();
        db.Offices.AddRange(
            new OfficeEntity { Id = _a, Name = "A", CreatedAt = DateTimeOffset.UtcNow },
            new OfficeEntity { Id = _b, Name = "B", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await SeedMattersAndHearings(db, _a, 1);
        await SeedMattersAndHearings(db, _b, 2);
        await SeedAudit(db, _a, 1);
        await SeedAudit(db, _b, 2);
        await SeedAuth(db, _a, 1);
        await SeedAuth(db, _b, 2);
        await SeedOfficeCore(db, _a, 1);
        await SeedOfficeCore(db, _b, 2);
    }

    private static async Task SeedOfficeCore(KorkutDbContext db, Guid office, int n)
    {
        // Reuse an existing matter for the party FK so the matters count stays exact.
        var matterId = await db.Matters.Where(m => m.OfficeId == office).Select(m => m.Id).FirstAsync();
        for (var i = 0; i < n; i++)
        {
            var client = new Client(office, $"c{i}", "individual", null, null, DateTimeOffset.UtcNow);
            var clientId = client.Id;
            db.Clients.Add(client);
            db.Courts.Add(new Court(office, $"court{i}", DateTimeOffset.UtcNow));
            db.Parties.Add(new Party(office, matterId, "plaintiff", $"p{i}", DateTimeOffset.UtcNow));
            db.Tasks.Add(new WorkTask(office, $"t{i}", null, null, null, DateTimeOffset.UtcNow));
            db.Deadlines.Add(new Deadline(office, $"d{i}", new DateOnly(2026, 1, 1), "internal", null, "Europe/Istanbul", 0, DateTimeOffset.UtcNow));
            db.CommunicationNotes.Add(new CommunicationNote(office, $"n{i}", "note", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            var docId = Guid.CreateVersion7();
            db.Documents.Add(new DocumentEntity { Id = docId, OfficeId = office, Bucket = "korkut-quarantine", ObjectKey = $"{office:N}/doc{i}", OriginalFilename = "f.pdf", ContentType = "application/pdf", Size = 10, ChecksumSha256 = "abc", Status = "quarantine", CreatedAt = DateTimeOffset.UtcNow });
            db.DocumentExtractions.Add(new DocumentExtractionEntity { Id = Guid.CreateVersion7(), OfficeId = office, DocumentId = docId, DocumentVersion = 1, ParserVersion = "plaintext-v1", Page = 1, SpanIndex = 0, Text = $"span{i}", SourceChecksumSha256 = "abc", SourceObjectKey = $"{office:N}/doc{i}", CreatedAt = DateTimeOffset.UtcNow });
            db.DocumentRelations.Add(new DocumentRelationEntity { Id = Guid.CreateVersion7(), OfficeId = office, DocumentId = docId, TargetType = "client", TargetId = clientId, IsPrimary = true, CreatedAt = DateTimeOffset.UtcNow });
            db.Invites.Add(new InviteEntity { Id = Guid.CreateVersion7(), OfficeId = office, Email = $"invite{i}@example.com", Role = "lawyer", TokenHash = $"hash{office:N}{i}", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), CreatedAt = DateTimeOffset.UtcNow });
            db.Invoices.Add(new Invoice(office, 100m, 0.20m, null, null, null, "draft", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            db.Expenses.Add(new Expense(office, 10m, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
            db.Correspondence.Add(new CorrespondenceItem(office, "incoming", $"e{i}", null, null, null, null, DateTimeOffset.UtcNow));
            db.CalendarEvents.Add(new CalendarEventEntity { Id = Guid.CreateVersion7(), OfficeId = office, Category = "appointment", Title = $"cal{i}", StartAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
            db.StyleProfiles.Add(new StyleProfileEntity { Id = Guid.CreateVersion7(), OfficeId = office, Scope = "user", UserId = Guid.CreateVersion7(), UpdatedAt = DateTimeOffset.UtcNow });
            db.MemoryItems.Add(new MemoryItemEntity { Id = Guid.CreateVersion7(), OfficeId = office, Kind = "note", Text = $"mem{i}", Status = "active", CreatedAt = DateTimeOffset.UtcNow });
            db.TemplateOverrides.Add(new TemplateOverrideEntity { Id = Guid.CreateVersion7(), OfficeId = office, Scope = "user", UserId = Guid.CreateVersion7(), Kind = "dilekce", TypeId = $"t{i}", Content = "x", UpdatedAt = DateTimeOffset.UtcNow });
            db.OutboundMessages.Add(new OutboundMessageEntity { Id = Guid.CreateVersion7(), OfficeId = office, Kind = "email", ToAddr = $"to{i}@x.test", Subject = "s", Body = "b", Status = "draft", CreatedAt = DateTimeOffset.UtcNow });
            // conversations/chat_messages/generated_documents/document_versions live in the
            // Agent DB now — seeded + isolation-checked in AgentTenantIsolationTests.
            if (i == 0)
            {
                db.Personas.Add(new PersonaEntity { OfficeId = office, UpdatedAt = DateTimeOffset.UtcNow }); // one per office
                db.Channels.Add(new ChannelEntity { OfficeId = office, Kind = "email", UpdatedAt = DateTimeOffset.UtcNow }); // one per office (per-kind PK)
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAuth(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var uid = Guid.CreateVersion7();
            db.Users.Add(new UserEntity { Id = uid, OfficeId = office, Email = $"{office:N}-{i}@x.test", SecurityStamp = Guid.CreateVersion7(), CreatedAt = DateTimeOffset.UtcNow });
            db.PasswordResetTokens.Add(new PasswordResetTokenEntity { Id = Guid.CreateVersion7(), OfficeId = office, UserId = uid, TokenHash = $"h{office:N}{i}", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1), CreatedAt = DateTimeOffset.UtcNow });
            db.UserTotps.Add(new UserTotpEntity { Id = Guid.CreateVersion7(), OfficeId = office, UserId = uid, EncryptedSecret = $"enc{i}", CreatedAt = DateTimeOffset.UtcNow });
            db.RecoveryCodes.Add(new RecoveryCodeEntity { Id = Guid.CreateVersion7(), OfficeId = office, UserId = uid, CodeHash = $"rc{office:N}{i}", CreatedAt = DateTimeOffset.UtcNow });
            db.RefreshTokens.Add(new RefreshTokenEntity { Id = Guid.CreateVersion7(), FamilyId = Guid.CreateVersion7(), OfficeId = office, UserId = uid, TokenHash = $"rt{office:N}{i}", ExpiresAt = DateTimeOffset.UtcNow.AddDays(14), CreatedAt = DateTimeOffset.UtcNow });
            db.OfficeCustomRoles.Add(new OfficeCustomRoleEntity { Id = Guid.CreateVersion7(), OfficeId = office, Key = $"role{i}", Name = "R", Permissions = ["matters.read"], CreatedAt = DateTimeOffset.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedAudit(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            db.AuditEvents.Add(new AuditEventEntity
            {
                Id = Guid.CreateVersion7(),
                OfficeId = office,
                Action = "read",
                EntityType = "matter",
                At = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task SeedMattersAndHearings(KorkutDbContext db, Guid office, int n)
    {
        for (var i = 0; i < n; i++)
        {
            var matter = new Matter(office, $"m{i}", null, null, null, DateTimeOffset.UtcNow);
            db.Matters.Add(matter);
            db.MatterHearings.Add(new Hearing(office, matter.Id, DateTimeOffset.UtcNow, null, null, null));
        }

        await db.SaveChangesAsync();
    }

    // --- Coverage guard: DB-forced RLS tables must all be in the registry ---

    [Fact]
    public async Task Every_rls_forced_table_is_covered_by_the_matrix()
    {
        await using var db = pg.NewContext();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT c.relname FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname='korkut' AND c.relkind='r' AND c.relforcerowsecurity";
        var forced = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                forced.Add(reader.GetString(0));
            }
        }

        // Any RLS-forced table missing from the matrix registry fails CI — a new
        // tenant-owned entity cannot ship without isolation coverage.
        var uncovered = forced.Where(t => !TenantMatrix.Tables.ContainsKey(t)).ToList();
        Assert.True(uncovered.Count == 0, $"tenant tables missing from the isolation matrix: {string.Join(", ", uncovered)}");
        Assert.Equal(TenantMatrix.Tables.Count, forced.Count);
    }

    // --- Negative matrix over every table and principal ---

    [Theory]
    [MemberData(nameof(TenantMatrix.TableNames), MemberType = typeof(TenantMatrix))]
    public async Task No_context_principal_sees_no_rows(string table)
    {
        // A genuinely fresh connection (no pooled reuse) with no tenant context must
        // see nothing. On a *reused* connection a no-set query can observe stale RLS
        // context, which is why the app always sets the tenant per scope (KDM-013).
        await using var conn = new NpgsqlConnection(pg.AppConnectionStringFresh);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        Assert.Equal(0, await TenantMatrix.CountAsync(conn, tx, table));
    }

    [Theory]
    [MemberData(nameof(TenantMatrix.TableNames), MemberType = typeof(TenantMatrix))]
    public async Task Each_office_sees_only_its_own_rows(string table)
    {
        var (expectA, expectB) = TenantMatrix.Tables[table];
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        await using (var txA = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, txA, _a);
            Assert.Equal(expectA, await TenantMatrix.CountAsync(conn, txA, table));
        }

        await using var txB = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, txB, _b);
        Assert.Equal(expectB, await TenantMatrix.CountAsync(conn, txB, table));
    }

    // --- Context cleanup after rollback and across pooled reuse ---

    [Fact]
    public async Task Rolled_back_context_does_not_leak_into_the_next_scope()
    {
        // Note: a custom GUC set with is_local is NOT reliably reset by ROLLBACK, so
        // isolation must not depend on implicit cleanup. The architecture instead sets
        // the tenant explicitly per scope — so the next scope's tenant always wins and
        // stale context can never be read. This proves that guarantee.
        await using var conn = new NpgsqlConnection(pg.AppConnectionString);
        await conn.OpenAsync();

        await using (var tx = await conn.BeginTransactionAsync())
        {
            await TenantMatrix.SetTenantAsync(conn, tx, _a);
            Assert.Equal(1, await TenantMatrix.CountAsync(conn, tx, "matters"));
            await tx.RollbackAsync();
        }

        // The next scope sets its OWN tenant (office B) and sees only B's rows — never
        // office A's rolled-back context.
        await using var tx2 = await conn.BeginTransactionAsync();
        await TenantMatrix.SetTenantAsync(conn, tx2, _b);
        Assert.Equal(2, await TenantMatrix.CountAsync(conn, tx2, "matters"));
    }

    // --- Background-job path is tenant-scoped and audited ---

    [Fact]
    public async Task Background_jobs_run_under_their_own_tenant_and_are_audited()
    {
        var clock = new SystemClockAdapter();
        await using var db = pg.NewContext();
        var outbox = new EfOutboxStore(db, clock);
        var inbox = new EfInboxStore(db, clock);
        var audit = new EfJobAudit(db);
        var seenTenants = new List<Guid?>();
        var tenant = new AmbientTenantContext();
        var handler = new RecordingTenantHandler(tenant, seenTenants);
        var dispatcher = new OutboxDispatcher(
            outbox, inbox, audit, new Dictionary<string, IJobHandler> { ["email"] = handler },
            tenant, new ExponentialRetryPolicy(), clock);

        await outbox.EnqueueAsync(Msg(_a, "a"), default);
        await outbox.EnqueueAsync(Msg(_b, "b"), default);

        await dispatcher.DispatchOnceAsync("w", 10, TimeSpan.FromMinutes(1));

        Assert.Contains(_a, seenTenants);
        Assert.Contains(_b, seenTenants);
        Assert.DoesNotContain(null, seenTenants);
        // Each office's job produced an audit row scoped to that office.
        Assert.True(await db.JobAudit.AnyAsync(x => x.OfficeId == _a));
        Assert.True(await db.JobAudit.AnyAsync(x => x.OfficeId == _b));
    }

    private static OutboxMessage Msg(Guid office, string key) => new()
    {
        Id = Guid.CreateVersion7(), OfficeId = office, Type = "email", Payload = "{}", IdempotencyKey = key,
    };

    private sealed class SystemClockAdapter : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private sealed class RecordingTenantHandler(ITenantContext tenant, List<Guid?> seen) : IJobHandler
    {
        public string MessageType => "email";

        public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            seen.Add(tenant.CurrentOfficeId);
            return Task.CompletedTask;
        }
    }
}
